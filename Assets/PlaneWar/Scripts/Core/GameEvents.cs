using System;

namespace PlaneWar
{
    public enum GameState
    {
        /// <summary>主界面（开始游戏）</summary>
        Home,
        Playing,
        Paused,
        /// <summary>玩家被击毁，爆炸动画播放中</summary>
        PlayerDying,
        GameOver,
    }

    /// <summary>全局事件总线：逻辑层只发事件，UI / 音效 / 平台层各自订阅，互不依赖。</summary>
    public static class GameEvents
    {
        public static event Action<GameState, GameState> StateChanged;   // (旧, 新)
        public static event Action<int> ScoreChanged;
        public static event Action<int> BombCountChanged;
        public static event Action<int> LivesChanged;
        public static event Action<int> LevelChanged;
        public static event Action<float> DoubleBulletChanged;           // 剩余秒数，0 = 结束
        public static event Action<EnemyKind, bool> EnemyKilled;         // (类型, 是否炸弹击杀)
        public static event Action<EnemyKind> EnemySpawned;
        public static event Action<SupplyKind> SupplyCollected;
        public static event Action BombUsed;
        public static event Action PlayerFired;
        public static event Action PlayerHit;

        public static void RaiseStateChanged(GameState from, GameState to) { if (StateChanged != null) StateChanged(from, to); }
        public static void RaiseScoreChanged(int v) { if (ScoreChanged != null) ScoreChanged(v); }
        public static void RaiseBombCountChanged(int v) { if (BombCountChanged != null) BombCountChanged(v); }
        public static void RaiseLivesChanged(int v) { if (LivesChanged != null) LivesChanged(v); }
        public static void RaiseLevelChanged(int v) { if (LevelChanged != null) LevelChanged(v); }
        public static void RaiseDoubleBulletChanged(float v) { if (DoubleBulletChanged != null) DoubleBulletChanged(v); }
        public static void RaiseEnemyKilled(EnemyKind k, bool byBomb) { if (EnemyKilled != null) EnemyKilled(k, byBomb); }
        public static void RaiseEnemySpawned(EnemyKind k) { if (EnemySpawned != null) EnemySpawned(k); }
        public static void RaiseSupplyCollected(SupplyKind k) { if (SupplyCollected != null) SupplyCollected(k); }
        public static void RaiseBombUsed() { if (BombUsed != null) BombUsed(); }
        public static void RaisePlayerFired() { if (PlayerFired != null) PlayerFired(); }
        public static void RaisePlayerHit() { if (PlayerHit != null) PlayerHit(); }

        /// <summary>重新进入 Play 模式（关闭 Domain Reload 时）清空残留订阅。</summary>
        public static void ClearAll()
        {
            StateChanged = null; ScoreChanged = null; BombCountChanged = null; LivesChanged = null;
            LevelChanged = null; DoubleBulletChanged = null; EnemyKilled = null; EnemySpawned = null;
            SupplyCollected = null; BombUsed = null; PlayerFired = null; PlayerHit = null;
        }
    }
}
