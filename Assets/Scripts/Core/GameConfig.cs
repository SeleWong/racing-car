// ---------------------------------------------------------------------------
// 微信飞机大战 · 复刻 —— 核心数值配置
// 所有数值与 GAME_SPEC.md 一一对应;客户端与服务器共用本文件。
// 纯 C#(无 UnityEngine 依赖),可被服务器工程直接编译。
// ---------------------------------------------------------------------------
using System;

namespace PlaneWar.Core
{
    public static class GameConfig
    {
        // ---- 世界 ----
        public const float WorldWidth = 9f;
        public const float WorldHeight = 16f;
        public const float TickRate = 60f;                    // 逻辑步进频率
        public const float Tick = 1f / TickRate;
        public const float DespawnBelowY = -2f;               // 敌机出屏回收线

        // ---- 玩家 ----
        public const int PlayerStartLives = 3;
        public const int PlayerMaxLives = 5;
        public const float PlayerHalfW = 0.55f;
        public const float PlayerHalfH = 0.60f;
        public const float PlayerMaxMoveSpeed = 13f;          // 单位/秒(拖拽速度上限)
        public const float DragSensitivity = 1.05f;
        public const float PlayerMinX = 0.7f;
        public const float PlayerMaxX = WorldWidth - 0.7f;
        public const float PlayerMinY = 0.9f;
        public const float PlayerMaxY = WorldHeight - 1.4f;
        public const float PlayerVisualYOffset = 0.9f;        // 渲染抬高(手指不遮挡)
        public const float PlayerDeathDuration = 0.9f;        // 死亡流程时长

        // ---- 玩家射击 ----
        public const float FireInterval = 1f / 6f;
        public const float PlayerBulletSpeed = 18f;
        public const int PlayerBulletDamage = 1;
        public const float PlayerBulletHalfW = 0.07f;
        public const float PlayerBulletHalfH = 0.22f;
        public const float DoubleShotDuration = 10f;
        public const float DoubleShotMuzzleOffsetX = 0.30f;

        // ---- 敌机(数值表) ----
        // index 与 EnemyKind 对齐:0 小型 1 中型 2 大型 3 BOSS
        public static readonly int[] EnemyHp = { 1, 4, 10, 30 };
        public static readonly int[] EnemyScore = { 5, 20, 50, 100 };
        public static readonly float[] EnemyHalfW = { 0.5f, 0.95f, 1.5f, 2.1f };
        public static readonly float[] EnemyHalfH = { 0.45f, 0.8f, 1.3f, 1.7f };
        public static readonly float[] EnemyBaseSpeed = { 3.2f, 1.9f, 1.0f, 0.9f };
        public static readonly float[] EnemySpeedGain = { 0.7f, 0.45f, 0.25f, 0.0f }; // 每点难度增速
        public static readonly float[] EnemyMaxSpeed = { 7.2f, 4.2f, 2.0f, 1.15f };
        public static readonly float[] EnemyFireIntervalBase = { 0f, 2.8f, 3.4f, 1.2f }; // 0=不射击
        public static readonly float[] EnemyFireIntervalMin = { 0f, 1.2f, 1.6f, 0.9f };
        public const float EnemyBulletSpeedBase = 3.6f;
        public const float EnemyBulletSpeedGain = 0.55f;      // 每点难度
        public const float EnemyBulletSpeedMax = 7f;
        public const float EnemyBulletSpeedLargeMul = 1.25f;
        public const float EnemyBulletHalfW = 0.10f;
        public const float EnemyBulletHalfH = 0.175f;
        public const float EnemyHitFlashTime = 0.08f;

        // ---- BOSS ----
        public const float BossHoldY = 12.5f;
        public const float BossHoldTime = 6.0f;

        // ---- 难度 ----
        public const float LevelDuration = 30f;               // 每关时长
        public const int DifficultyMaxLevelSpan = 8;          // 8 关内拉满难度
        public const float EnemySpeedLevelScale = 0.10f;      // 每关基础速度 +10%

        // ---- 波次表(每 30 秒一关,循环使用) ----
        // 每项: {波间隔秒, 小型, 中型, 大型, BOSS}
        public static readonly float[,] WaveTable =
        {
            { 0f,   5, 0, 0, 0 },
            { 4.0f, 4, 0, 0, 0 },
            { 5.5f, 0, 2, 0, 0 },
            { 5.5f, 7, 0, 0, 0 },
            { 6.5f, 4, 2, 0, 0 },
            { 7.5f, 3, 0, 1, 0 },
            { 8.5f, 0, 3, 0, 0 },
            { 9.5f, 9, 0, 0, 0 },
            { 10.5f, 0, 3, 1, 0 },
            { 12.0f, 2, 0, 0, 1 },
        };
        public const int WaveCount = 10;

        // ---- 道具 ----
        public const float PowerupSpeed = 2.6f;
        public const float PowerupHalf = 0.55f;
        public const float BombDamageToBoss = 5f;
        // 掉落率:行=道具(0双倍 1炸弹 2加血),列=来源(0中型 1大型 2BOSS)
        public static readonly float[,] DropChance =
        {
            { 0.12f, 0.30f, 1.00f },
            { 0.06f, 0.22f, 0.00f },
            { 0.00f, 0.25f, 0.00f },
        };

        // ---- 计分 ----
        public const int ScorePerDistanceTick = 1;
        public const float DistanceScoreFreqMin = 0.25f;      // 第 1 关 每4秒1分
        public const float DistanceScoreFreqMax = 0.75f;      // 高关卡 每1.33秒1分
    }

    /// <summary>敌机种类,数值与 GameConfig 表索引一致。</summary>
    public enum EnemyKind : byte { Small = 0, Medium = 1, Large = 2, Boss = 3 }

    /// <summary>道具种类。</summary>
    public enum PowerupKind : byte { DoubleShot = 0, Bomb = 1, Life = 2 }

    /// <summary>游戏阶段。</summary>
    public enum GamePhase : byte { Ready = 0, Playing = 1, GameOver = 2 }

    /// <summary>服务器→客户端事件(见 GAME_SPEC §10)。</summary>
    public enum NetEvent : byte
    {
        GameStart = 1,
        GameOver = 2,
        PlayerHit = 3,
        PlayerDead = 4,
        EnemyKilled = 5,
        BossKilled = 6,
        PowerupPicked = 7,
        BombBlast = 8,
        PlayerFired = 9,
        EnemyFired = 10,
    }
}
