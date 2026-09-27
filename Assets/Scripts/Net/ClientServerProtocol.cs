// ---------------------------------------------------------------------------
// 客户端-服务器协议编解码(帧格式见 GAME_SPEC §10)。
// 帧 = [1 字节消息类型][载荷];全部小端。服务器工程同样引用本文件。
// ---------------------------------------------------------------------------
using System;
using System.IO;
using PlaneWar.Core;

namespace PlaneWar.Net
{
    public static class MsgType
    {
        public const byte Join = 0x01;
        public const byte JoinAck = 0x02;
        public const byte Input = 0x03;
        public const byte Snapshot = 0x04;
        public const byte Event = 0x05;
        public const byte Reset = 0x06;
        public const byte ResetAck = 0x07;
    }

    /// <summary>客户端发出的输入帧(拖拽目标点,世界坐标)。</summary>
    public struct InputMsg
    {
        public uint Seq;
        public bool HasPointer;
        public float X, Y;
    }

    /// <summary>服务器发出的事件帧。</summary>
    public struct EventMsg
    {
        public NetEvent Type;
        public float X, Y;
        public int IntParam;
        public int EntityId;
    }

    public static class Protocol
    {
        // ------------------------- 编码 -------------------------

        public static byte[] EncodeJoin()
        {
            return new byte[] { MsgType.Join };
        }

        public static byte[] EncodeReset()
        {
            return new byte[] { MsgType.Reset };
        }

        public static byte[] EncodeInput(InputMsg msg)
        {
            using (var ms = new MemoryStream(16))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(MsgType.Input);
                w.Write(msg.Seq);
                w.Write((byte)(msg.HasPointer ? 1 : 0));
                w.Write(msg.X);
                w.Write(msg.Y);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static byte[] EncodeJoinAck(uint roomId)
        {
            var buf = new byte[5];
            buf[0] = MsgType.JoinAck;
            buf[1] = (byte)(roomId & 0xFF);
            buf[2] = (byte)((roomId >> 8) & 0xFF);
            buf[3] = (byte)((roomId >> 16) & 0xFF);
            buf[4] = (byte)((roomId >> 24) & 0xFF);
            return buf;
        }

        public static byte[] EncodeResetAck()
        {
            return new byte[] { MsgType.ResetAck };
        }

        /// <summary>快照帧 = 类型 + SnapshotCodec.Write。</summary>
        public static byte[] EncodeSnapshot(GameCore core)
        {
            using (var ms = new MemoryStream(1024))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(MsgType.Snapshot);
                SnapshotCodec.Write(w, core);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static byte[] EncodeEvent(EventMsg msg)
        {
            using (var ms = new MemoryStream(24))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(MsgType.Event);
                w.Write((byte)msg.Type);
                w.Write(msg.X);
                w.Write(msg.Y);
                w.Write(msg.IntParam);
                w.Write(msg.EntityId);
                w.Flush();
                return ms.ToArray();
            }
        }

        // ------------------------- 解码 -------------------------

        public static InputMsg DecodeInput(byte[] frame)
        {
            using (var r = new BinaryReader(new MemoryStream(frame, 1, frame.Length - 1)))
            {
                return new InputMsg
                {
                    Seq = r.ReadUInt32(),
                    HasPointer = r.ReadByte() != 0,
                    X = r.ReadSingle(),
                    Y = r.ReadSingle(),
                };
            }
        }

        public static uint DecodeJoinAck(byte[] frame)
        {
            return (uint)(frame[1] | (frame[2] << 8) | (frame[3] << 16) | (frame[4] << 24));
        }

        /// <summary>解码快照到复用对象,避免 GC。</summary>
        public static SnapshotData DecodeSnapshot(byte[] frame, SnapshotData reuse)
        {
            using (var r = new BinaryReader(new MemoryStream(frame, 1, frame.Length - 1)))
            {
                var snap = SnapshotCodec.Read(r);
                if (reuse == null || snap == null) return snap;
                // 拷贝进复用对象
                reuse.Clear();
                reuse.Tick = snap.Tick; reuse.Score = snap.Score;
                reuse.HighScore = snap.HighScore; reuse.Kills = snap.Kills;
                reuse.Lives = snap.Lives; reuse.Phase = snap.Phase;
                reuse.DoubleShotLeft = snap.DoubleShotLeft;
                reuse.PlayerAlive = snap.PlayerAlive; reuse.DeadTimer = snap.DeadTimer;
                reuse.Time = snap.Time;
                reuse.PlayerX = snap.PlayerX; reuse.PlayerY = snap.PlayerY;
                reuse.Enemies.AddRange(snap.Enemies);
                reuse.PlayerBullets.AddRange(snap.PlayerBullets);
                reuse.EnemyBullets.AddRange(snap.EnemyBullets);
                reuse.Powerups.AddRange(snap.Powerups);
                return reuse;
            }
        }

        public static EventMsg DecodeEvent(byte[] frame)
        {
            using (var r = new BinaryReader(new MemoryStream(frame, 1, frame.Length - 1)))
            {
                return new EventMsg
                {
                    Type = (NetEvent)r.ReadByte(),
                    X = r.ReadSingle(),
                    Y = r.ReadSingle(),
                    IntParam = r.ReadInt32(),
                    EntityId = r.ReadInt32(),
                };
            }
        }
    }
}
