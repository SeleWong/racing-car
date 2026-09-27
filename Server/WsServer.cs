// ---------------------------------------------------------------------------
// WsServer —— 基于 HttpListener 的 WebSocket 服务器(无需第三方库)。
// 每条连接:接收任务(解码帧→投递房间)+发送任务(出站队列→网络)。
// ---------------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using PlaneWar.Net;

namespace PlaneWar.Server
{
    public enum ClientMsgKind { Input, Reset }

    public struct ClientMsg
    {
        public ClientMsgKind Kind;
        public InputMsg Input;
    }

    /// <summary>一条已接入的客户端连接。</summary>
    public sealed class ClientConn
    {
        public readonly Guid Id = Guid.NewGuid();
        private readonly ConcurrentQueue<byte[]> _outbox = new ConcurrentQueue<byte[]>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private WebSocket _socket;
        private volatile bool _closed;

        public void Enqueue(byte[] frame)
        {
            if (_closed) return;
            _outbox.Enqueue(frame);
            _signal.Release();
        }

        public void MarkClosed()
        {
            _closed = true;
            try { _signal.Release(); } catch { /* already disposed */ }
        }

        /// <summary>每连接的收发循环。</summary>
        public async Task Run(WebSocket socket, Room room)
        {
            _socket = socket;
            var recvTask = ReceiveLoop(socket, room);
            var sendTask = SendLoop(socket);
            await Task.WhenAny(recvTask, sendTask).ConfigureAwait(false);
            _closed = true;
            room.RemoveClient(Id);
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye",
                                            new CancellationTokenSource(1000).Token)
                                .ConfigureAwait(false);
                }
            }
            catch { /* 连接可能已断 */ }
        }

        private async Task ReceiveLoop(WebSocket socket, Room room)
        {
            var buf = new byte[4096];
            bool joined = false;
            try
            {
                while (socket.State == WebSocketState.Open && !_closed)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buf),
                                                           CancellationToken.None).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    if (!result.EndOfMessage || result.Count < 1) continue;

                    switch (buf[0])
                    {
                        case MsgType.Join:
                            if (!joined) { joined = true; room.AddClient(this); }
                            break;
                        case MsgType.Input:
                            if (joined)
                            {
                                var frame = new byte[result.Count];
                                Array.Copy(buf, frame, result.Count);
                                room.Post(new ClientMsg
                                {
                                    Kind = ClientMsgKind.Input,
                                    Input = Protocol.DecodeInput(frame),
                                });
                            }
                            break;
                        case MsgType.Reset:
                            if (joined) room.Post(new ClientMsg { Kind = ClientMsgKind.Reset });
                            break;
                    }
                }
            }
            catch { /* 断线即退出 */ }
        }

        private async Task SendLoop(WebSocket socket)
        {
            try
            {
                while (socket.State == WebSocketState.Open && !_closed)
                {
                    await _signal.WaitAsync().ConfigureAwait(false);
                    if (_closed) return;
                    byte[] frame;
                    while (_outbox.TryDequeue(out frame))
                    {
                        if (socket.State != WebSocketState.Open) return;
                        await socket.SendAsync(new ArraySegment<byte>(frame),
                                               WebSocketMessageType.Binary, true,
                                               CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
            catch { /* 断线即退出 */ }
        }
    }

    public sealed class WsServer
    {
        private readonly HttpListener _listener;
        private readonly Room _room;

        public WsServer(string prefix, uint seed)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _room = new Room(seed);
        }

        public void Start()
        {
            _listener.Start();
            _room.Start();
            Task.Run(AcceptLoop);
        }

        private async Task AcceptLoop()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch
                {
                    return; // listener 已停止
                }

                if (!ctx.Request.IsWebSocketRequest)
                {
                    // 普通 HTTP 请求:返回状态页,便于部署探活
                    var bytes = System.Text.Encoding.UTF8.GetBytes(
                        "PlaneWar GameServer is running. Connect via WebSocket.\n");
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/plain";
                    await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    ctx.Response.Close();
                    continue;
                }

                HttpListenerWebSocketContext wsCtx;
                try
                {
                    wsCtx = await ctx.AcceptWebSocketAsync(null).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[WsServer] accept failed: {0}", ex.Message);
                    continue;
                }
                var conn = new ClientConn();
                _ = conn.Run(wsCtx.WebSocket, _room); // 每连接独立收发
            }
        }
    }
}
