// ---------------------------------------------------------------------------
// Room —— 权威对局:
//   · 单线程 60Hz 驱动与客户端完全相同的 GameCore(共用源码,规则天然一致);
//   · 20Hz 向全部接入端广播世界快照;事件即时广播;
//   · 输入取"最新值覆盖"(拖拽目标点语义,无需回滚);
//   · 任一端请求重开 → 整局重置并广播。
// ---------------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PlaneWar.Core;
using PlaneWar.Net;

namespace PlaneWar.Server
{
    public sealed class Room
    {
        private readonly GameCore _core;
        private readonly ConcurrentDictionary<Guid, ClientConn> _clients =
            new ConcurrentDictionary<Guid, ClientConn>();
        private readonly ConcurrentQueue<ClientMsg> _inbox = new ConcurrentQueue<ClientMsg>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly object _snapshotLock = new object();
        private InputMsg _pendingInput;
        private bool _hasPendingInput;
        private bool _resetRequested;
        private Task _loop;

        public Room(uint seed)
        {
            _core = new GameCore(seed, 0);
        }

        public int ClientCount { get { return _clients.Count; } }

        public void Start()
        {
            _loop = Task.Run(RunLoop);
        }

        public void Stop()
        {
            _cts.Cancel();
            try { if (_loop != null) _loop.Wait(2000); } catch { /* ignore */ }
        }

        // ------------------------- 客户端接入 -------------------------

        public void AddClient(ClientConn client)
        {
            _clients[client.Id] = client;
            client.Enqueue(Protocol.EncodeJoinAck((uint)Environment.TickCount));
            // 首个玩家进入:自动开局;已有对局则直接加入(观战/续玩)
            lock (_snapshotLock)
            {
                if (_core.Phase == GamePhase.Ready) _core.BeginGame();
            }
            Console.WriteLine("[Room] client joined, total={0}", _clients.Count);
        }

        public void RemoveClient(Guid id)
        {
            ClientConn removed;
            _clients.TryRemove(id, out removed);
            Console.WriteLine("[Room] client left, total={0}", _clients.Count);
        }

        /// <summary>由连接线程投递输入/重置请求。</summary>
        public void Post(ClientMsg msg)
        {
            _inbox.Enqueue(msg);
        }

        // ------------------------- 主循环 -------------------------

        private void RunLoop()
        {
            const double tickMs = 1000.0 / GameConfig.TickRate;   // 60Hz 逻辑
            const double snapMs = 50.0;                            // 20Hz 快照
            var sw = Stopwatch.StartNew();
            double nextTick = tickMs;
            double nextSnap = snapMs;

            while (!_cts.IsCancellationRequested)
            {
                double now = sw.ElapsedMilliseconds;

                if (now >= nextTick)
                {
                    // 防止后台挂起后追赶过多步
                    if (now - nextTick > 500.0) nextTick = now;

                    ProcessInbox();
                    lock (_snapshotLock)
                    {
                        if (_hasPendingInput)
                        {
                            _core.SetInput(new PlayerInput
                            {
                                HasPointer = _pendingInput.HasPointer,
                                X = _pendingInput.X,
                                Y = _pendingInput.Y,
                            });
                        }
                        _core.SimulateStep();
                        BroadcastEvents();
                    }
                    _hasPendingInput = false;
                    nextTick += tickMs;
                }

                if (now >= nextSnap)
                {
                    BroadcastSnapshot();
                    nextSnap = now + snapMs;
                }

                Thread.Sleep(1);
            }
        }

        private void ProcessInbox()
        {
            ClientMsg msg;
            while (_inbox.TryDequeue(out msg))
            {
                switch (msg.Kind)
                {
                    case ClientMsgKind.Input:
                        _pendingInput = msg.Input;
                        _hasPendingInput = true;
                        break;
                    case ClientMsgKind.Reset:
                        _resetRequested = true;
                        break;
                }
            }
            if (_resetRequested)
            {
                lock (_snapshotLock)
                {
                    _core.Restart();
                    _core.BeginGame();
                }
                _resetRequested = false;
                Broadcast(Protocol.EncodeResetAck());
                Console.WriteLine("[Room] game reset");
            }
        }

        private void BroadcastEvents()
        {
            var evs = _core.PendingEvents;
            if (evs.Count == 0 || _clients.IsEmpty) { evs.Clear(); return; }
            for (int i = 0; i < evs.Count; i++)
            {
                var e = evs[i];
                var frame = Protocol.EncodeEvent(new EventMsg
                {
                    Type = e.Type, X = e.X, Y = e.Y,
                    IntParam = e.IntParam, EntityId = e.EntityId,
                });
                Broadcast(frame);
            }
            evs.Clear();
        }

        private void BroadcastSnapshot()
        {
            if (_clients.IsEmpty) return;
            byte[] frame;
            lock (_snapshotLock)
            {
                frame = Protocol.EncodeSnapshot(_core);
            }
            Broadcast(frame);
        }

        private void Broadcast(byte[] frame)
        {
            foreach (var kv in _clients)
            {
                kv.Value.Enqueue(frame);
            }
        }
    }
}
