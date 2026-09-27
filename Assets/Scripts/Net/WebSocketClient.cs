// ---------------------------------------------------------------------------
// WebSocket 通道实现(基于 System.Net.WebSockets,全平台可用;
// WebGL 需替换为浏览器 WebSocket 的 JS 桥接实现,接口见 IClientChannel)。
// 接收循环在后台线程,收到的整帧入队,Unity 主线程 Pump 时取出 —— 线程安全。
// ---------------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace PlaneWar.Net
{
    /// <summary>客户端网络通道抽象(便于替换实现 / 接入不同端)。</summary>
    public interface IClientChannel : IDisposable
    {
        void Connect(string url);
        void Send(byte[] frame);
        bool TryDequeue(out byte[] frame);
        bool IsOpen { get; }
        bool IsConnecting { get; }
        string LastError { get; }
        void Close();
    }

    public sealed class WebSocketClient : IClientChannel
    {
        private ClientWebSocket _ws;
        private readonly ConcurrentQueue<byte[]> _incoming = new ConcurrentQueue<byte[]>();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly byte[] _recvBuf = new byte[65536];
        private CancellationTokenSource _cts;
        private volatile bool _open;
        private volatile bool _connecting;
        private volatile string _error;

        public bool IsOpen { get { return _open; } }
        public bool IsConnecting { get { return _connecting; } }
        public string LastError { get { return _error; } }

        public void Connect(string url)
        {
            Close();
            _error = null;
            _connecting = true;
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            var ws = _ws;
            var cts = _cts;
            Task.Run(async () =>
            {
                try
                {
                    await ws.ConnectAsync(new Uri(url), cts.Token).ConfigureAwait(false);
                    if (cts.IsCancellationRequested) return;
                    _open = true;
                    _connecting = false;
                    await ReceiveLoop(ws, cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _error = ex.Message;
                }
                finally
                {
                    _open = false;
                    _connecting = false;
                }
            });
        }

        private async Task ReceiveLoop(ClientWebSocket ws, CancellationToken token)
        {
            var ms = new MemoryStream();
            while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(_recvBuf), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    ms.Write(_recvBuf, 0, result.Count);
                    if (ms.Length > 1 << 20) return; // 单帧上限 1MB,防御性保护
                } while (!result.EndOfMessage);

                _incoming.Enqueue(ms.ToArray());
            }
        }

        public void Send(byte[] frame)
        {
            var ws = _ws;
            if (ws == null || !_open) return;
            _sendLock.Wait();
            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    ws.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true,
                                 CancellationToken.None).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                _open = false;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public bool TryDequeue(out byte[] frame)
        {
            return _incoming.TryDequeue(out frame);
        }

        public void Close()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            var ws = _ws;
            _ws = null;
            _open = false;
            _connecting = false;
            if (ws != null)
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                          .GetAwaiter().GetResult();
                    }
                }
                catch { /* 忽略关闭异常 */ }
                ws.Dispose();
            }
        }

        public void Dispose()
        {
            Close();
            _sendLock.Dispose();
        }
    }
}
