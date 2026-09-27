// ---------------------------------------------------------------------------
// 快照序列化 —— 服务器把 GameCore 状态编码为二进制快照;
// 客户端解码为 SnapshotData 用于插值表现。两端共用本文件,字节级同构。
// 协议定义见 GAME_SPEC §10。全部小端(与 BinaryWriter 默认一致)。
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;

namespace PlaneWar.Core
{
    /// <summary>客户端侧的只读世界快照(插值用)。</summary>
    public sealed class SnapshotData
    {
        public struct EnemySnap
        {
            public int Id; public byte Kind; public short Hp; public short MaxHp;
            public float X, Y; public byte State;
        }
        public struct BulletSnap
        {
            public int Id; public float X, Y; public float VX, VY;
        }
        public struct PowerupSnap
        {
            public int Id; public byte Kind; public float X, Y;
        }

        public uint Tick;
        public int Score, HighScore, Kills;
        public byte Lives;
        public byte Phase;
        public float DoubleShotLeft;
        public byte PlayerAlive;
        public float DeadTimer;
        public float Time;
        public float PlayerX, PlayerY;

        public readonly List<EnemySnap> Enemies = new List<EnemySnap>();
        public readonly List<BulletSnap> PlayerBullets = new List<BulletSnap>();
        public readonly List<BulletSnap> EnemyBullets = new List<BulletSnap>();
        public readonly List<PowerupSnap> Powerups = new List<PowerupSnap>();

        public void Clear()
        {
            Enemies.Clear(); PlayerBullets.Clear(); EnemyBullets.Clear(); Powerups.Clear();
        }
    }

    public static class SnapshotCodec
    {
        /// <summary>服务器侧:把当前世界状态写入快照。</summary>
        public static void Write(BinaryWriter w, GameCore core)
        {
            w.Write((uint)core.Tick);
            w.Write(core.Score);
            w.Write(core.HighScore);
            w.Write(core.Kills);
            w.Write((byte)core.Lives);
            w.Write((byte)core.Phase);
            w.Write(core.DoubleShotLeft);
            w.Write((byte)(core.PlayerAlive ? 1 : 0));
            w.Write(core.DeadTimer);
            w.Write(core.Time);
            w.Write(core.PlayerX);
            w.Write(core.PlayerY);

            w.Write((ushort)core.Enemies.Count);
            for (int i = 0; i < core.Enemies.Count; i++)
            {
                Enemy e = core.Enemies[i];
                w.Write(e.Id);
                w.Write((byte)e.Kind);
                w.Write((short)e.Hp);
                w.Write((short)e.MaxHp);
                w.Write(e.X);
                w.Write(e.Y);
                w.Write(e.State);
            }

            w.Write((ushort)core.PlayerBullets.Count);
            for (int i = 0; i < core.PlayerBullets.Count; i++)
            {
                Bullet b = core.PlayerBullets[i];
                w.Write(b.Id); w.Write(b.X); w.Write(b.Y);
            }

            w.Write((ushort)core.EnemyBullets.Count);
            for (int i = 0; i < core.EnemyBullets.Count; i++)
            {
                Bullet b = core.EnemyBullets[i];
                w.Write(b.Id); w.Write(b.X); w.Write(b.Y); w.Write(b.VX); w.Write(b.VY);
            }

            w.Write((ushort)core.Powerups.Count);
            for (int i = 0; i < core.Powerups.Count; i++)
            {
                Powerup p = core.Powerups[i];
                w.Write(p.Id); w.Write((byte)p.Kind); w.Write(p.X); w.Write(p.Y);
            }
        }

        /// <summary>客户端侧:解码快照。返回 null 表示数据不完整。</summary>
        public static SnapshotData Read(BinaryReader r)
        {
            var snap = new SnapshotData();
            snap.Tick = r.ReadUInt32();
            snap.Score = r.ReadInt32();
            snap.HighScore = r.ReadInt32();
            snap.Kills = r.ReadInt32();
            snap.Lives = r.ReadByte();
            snap.Phase = r.ReadByte();
            snap.DoubleShotLeft = r.ReadSingle();
            snap.PlayerAlive = r.ReadByte();
            snap.DeadTimer = r.ReadSingle();
            snap.Time = r.ReadSingle();
            snap.PlayerX = r.ReadSingle();
            snap.PlayerY = r.ReadSingle();

            int n = r.ReadUInt16();
            for (int i = 0; i < n; i++)
            {
                var e = new SnapshotData.EnemySnap
                {
                    Id = r.ReadInt32(),
                    Kind = r.ReadByte(),
                    Hp = r.ReadInt16(),
                    MaxHp = r.ReadInt16(),
                    X = r.ReadSingle(),
                    Y = r.ReadSingle(),
                    State = r.ReadByte(),
                };
                snap.Enemies.Add(e);
            }

            n = r.ReadUInt16();
            for (int i = 0; i < n; i++)
            {
                var b = new SnapshotData.BulletSnap { Id = r.ReadInt32(), X = r.ReadSingle(), Y = r.ReadSingle() };
                snap.PlayerBullets.Add(b);
            }

            n = r.ReadUInt16();
            for (int i = 0; i < n; i++)
            {
                var b = new SnapshotData.BulletSnap
                {
                    Id = r.ReadInt32(),
                    X = r.ReadSingle(), Y = r.ReadSingle(),
                    VX = r.ReadSingle(), VY = r.ReadSingle(),
                };
                snap.EnemyBullets.Add(b);
            }

            n = r.ReadUInt16();
            for (int i = 0; i < n; i++)
            {
                var p = new SnapshotData.PowerupSnap
                {
                    Id = r.ReadInt32(),
                    Kind = r.ReadByte(),
                    X = r.ReadSingle(), Y = r.ReadSingle(),
                };
                snap.Powerups.Add(p);
            }
            return snap;
        }
    }
}
