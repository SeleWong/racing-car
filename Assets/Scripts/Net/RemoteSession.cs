// ---------------------------------------------------------------------------
// RemoteSession —— 联机会话(客户端侧):
//   · 维护 WebSocket 通道生命周期(连接 / 断线);
//   · 每帧 Pump:派发 JoinAck / Snapshot / Event / ResetAck;
//   · 保存带时间戳的快照缓冲(插值用)与事件队列;
//   · 发送输入帧 / 重开请求。
// 仅允许在 Unity 主线程调用(时间戳取 Time.realtimeSinceStartup)。
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace PlaneWar.Net
{
    public enum SessionState { Idle, Connecting, Connected, Disconnected }

    public sealed class RemoteSession
    {
        public struct SnapEntry
        {
            public float ReceivedAt;      // Time.realtimeSinceStartup
            public Core.SnapshotData Data;
        }

        private readonly IClientChannel _channel;
        private readonly List<SnapEntry> _snapshots = new List<SnapEntry>(64);
        private readonly Queue<EventMsg> _events = new Queue<EventMsg>(32);
        private uint _inputSeq;

        public SessionState State { get; private set; }
        public uint RoomId { get; private set; }
        public string LastError { get { return _channel.LastError; } }
        public bool ResetAcked { get; private set; }
        public float LastSnapshotAt { get; private set; }
        public Core.SnapshotData Latest { get; private set; }
        public IReadOnlyList<SnapEntry> Snapshots { get { return _snapshots; } }

        /// <summary>快照缓冲最多保留的秒数(超出裁掉)。</summary>
        public const float BufferSeconds = 2.5f;

        public RemoteSession(IClientChannel channel)
        {
            _channel = channel;
        }

        public void Start(string url)
        {
            Stop();
            State = SessionState.Connecting;
            _snapshots.Clear();
            _events.Clear();
            _inputSeq = 0;
            ResetAcked = false;
            Latest = null;
            LastSnapshotAt = 0f;
            _channel.Connect(url);
        }

        public void Stop()
        {
            if (State != SessionState.Idle) _channel.Close();
            State = SessionState.Idle;
        }

        /// <summary>每帧调用:推进连接状态、派发到达的帧。</summary>
        public void Pump()
        {
            switch (State)
            {
                case SessionState.Connecting:
                    if (_channel.IsOpen)
                    {
                        _channel.Send(Protocol.EncodeJoin());
                        State = SessionState.Connected; // JoinAck 到达后确认房间号
                    }
                    else if (!_channel.IsConnecting && _channel.LastError != null)
                    {
                        State = SessionState.Disconnected;
                    }
                    break;
                case SessionState.Connected:
                    if (!_channel.IsOpen && !_channel.IsConnecting)
                    {
                        State = SessionState.Disconnected;
                    }
                    break;
            }

            byte[] frame;
            while (_channel.TryDequeue(out frame))
            {
                if (frame.Length == 0) continue;
                switch (frame[0])
                {
                    case MsgType.JoinAck:
                        RoomId = Protocol.DecodeJoinAck(frame);
                        break;
                    case MsgType.Snapshot:
                        var snap = Protocol.DecodeSnapshot(frame, null);
                        if (snap != null)
                        {
                            Latest = snap;
                            LastSnapshotAt = Time.realtimeSinceStartup;
                            _snapshots.Add(new SnapEntry { ReceivedAt = LastSnapshotAt, Data = snap });
                        }
                        break;
                    case MsgType.Event:
                        _events.Enqueue(Protocol.DecodeEvent(frame));
                        break;
                    case MsgType.ResetAck:
                        ResetAcked = true;
                        _snapshots.Clear();
                        break;
                }
            }

            // 裁剪过期快照
            float cutoff = Time.realtimeSinceStartup - BufferSeconds;
            int keep = 0;
            while (keep < _snapshots.Count && _snapshots[keep].ReceivedAt < cutoff) keep++;
            if (keep > 0) _snapshots.RemoveRange(0, keep);
        }

        public void SendInput(bool hasPointer, float x, float y)
        {
            if (State != SessionState.Connected) return;
            _inputSeq++;
            _channel.Send(Protocol.EncodeInput(new InputMsg
            {
                Seq = _inputSeq,
                HasPointer = hasPointer,
                X = x,
                Y = y,
            }));
        }

        public void RequestReset()
        {
            if (State != SessionState.Connected) return;
            ResetAcked = false;
            _channel.Send(Protocol.EncodeReset());
        }

        public bool PollEvent(out EventMsg msg)
        {
            if (_events.Count == 0) { msg = default(EventMsg); return false; }
            msg = _events.Dequeue();
            return true;
        }
    }
}
