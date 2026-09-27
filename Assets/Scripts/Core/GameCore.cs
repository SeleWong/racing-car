// ---------------------------------------------------------------------------
// GameCore —— 微信飞机大战完整规则的确定性仿真内核(60Hz)。
// 纯 C#、无 UnityEngine 依赖、无静态可变状态:
//   · 客户端单机模式直接驱动它;
//   · 服务器权威模式驱动同一份代码,客户端只做表现。
// 所有规则与数值见 GAME_SPEC.md。
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace PlaneWar.Core
{
    // ------------------------- 实体数据 -------------------------

    public struct Enemy
    {
        public int Id;
        public EnemyKind Kind;
        public int Hp;
        public int MaxHp;
        public float X, Y;
        public float Speed;
        public float FireTimer;
        public float FireInterval;
        public float Flash;        // 受击白闪剩余时间
        public byte State;         // 0 下压  1 BOSS进场  2 BOSS悬停  3 BOSS俯冲
        public float HoldTimer;    // BOSS 悬停计时
        public float SwayPhase;    // 小型机摆动相位
    }

    public struct Bullet
    {
        public int Id;
        public float X, Y;
        public float VX, VY;
    }

    public struct Powerup
    {
        public int Id;
        public PowerupKind Kind;
        public float X, Y;
    }

    public struct GameEvent
    {
        public NetEvent Type;
        public float X, Y;
        public int IntParam;       // 含义随事件:敌机种类 / 得分 / 剩余生命
        public int EntityId;
    }

    /// <summary>每步输入(拖拽目标点,世界坐标)。</summary>
    public struct PlayerInput
    {
        public bool HasPointer;
        public float X, Y;
    }

    // ------------------------- 仿真内核 -------------------------

    public sealed class GameCore
    {
        // ---- 静态配置入口 ----
        public static GameCore Instance { get; private set; }

        // ---- 状态 ----
        public uint Seed;
        public FixedRandom Rng;
        public int PlayCount;
        public GamePhase Phase = GamePhase.Ready;
        public long Tick;
        public float Time;                       // 本局游戏时间(秒)
        public int Score;
        public int HighScore;
        public int Kills;
        public int Lives;
        public float DoubleShotLeft;             // 双倍火力剩余秒数

        // 玩家
        public bool PlayerAlive = true;
        public float PlayerX, PlayerY;
        public float PlayerTargetX, PlayerTargetY;
        public bool HasTarget;
        public float FireTimer;
        public float DistanceTimer;
        public float DeadTimer;                  // 死亡流程倒计时

        // 波次
        public float WaveTimer;
        public int WaveIndex;                    // 下一次要生成的波
        public int Level = 1;
        public float Difficulty;

        // 实体
        public readonly List<Enemy> Enemies = new List<Enemy>(64);
        public readonly List<Bullet> PlayerBullets = new List<Bullet>(64);
        public readonly List<Bullet> EnemyBullets = new List<Bullet>(64);
        public readonly List<Powerup> Powerups = new List<Powerup>(8);

        /// <summary>本步产生的事件(消费方每步清空)。</summary>
        public readonly List<GameEvent> PendingEvents = new List<GameEvent>(16);

        private int _nextId = 1;
        private PlayerInput _input;
        private bool _gameOverEmitted;

        public GameCore(uint seed, int highScore)
        {
            Seed = seed;
            HighScore = highScore;
            Rng = new FixedRandom(seed);
            Instance = this;
            ResetPlayerPose();
        }

        private void ResetPlayerPose()
        {
            PlayerX = GameConfig.WorldWidth * 0.5f;
            PlayerY = 2.4f;
            PlayerTargetX = PlayerX;
            PlayerTargetY = PlayerY;
        }

        /// <summary>整局重置(重新开始):换新随机流,其余清零,回到 Ready。</summary>
        public void Restart()
        {
            PlayCount++;
            Rng = new FixedRandom(Seed ^ (uint)(PlayCount * 0x9E3779B9u));
            Phase = GamePhase.Ready;
            Tick = 0; Time = 0; Score = 0; Kills = 0;
            Lives = GameConfig.PlayerStartLives;
            DoubleShotLeft = 0;
            PlayerAlive = true;
            FireTimer = 0; DistanceTimer = 0; DeadTimer = 0;
            WaveTimer = 0; WaveIndex = 0; Level = 1; Difficulty = 0;
            Enemies.Clear(); PlayerBullets.Clear(); EnemyBullets.Clear(); Powerups.Clear();
            PendingEvents.Clear();
            HasTarget = false;
            _gameOverEmitted = false;
            _nextId = 1;
            ResetPlayerPose();
        }

        /// <summary>Ready → Playing,立即生成第一波。</summary>
        public void BeginGame()
        {
            if (Phase != GamePhase.Ready) return;
            Phase = GamePhase.Playing;
            Emit(NetEvent.GameStart, PlayerX, PlayerY, 0, 0);
            SpawnWave(0);
            WaveIndex = 1;
            WaveTimer = DelayAfterWave(0);
        }

        /// <summary>写入下一步输入。</summary>
        public void SetInput(PlayerInput input)
        {
            _input = input;
        }

        /// <summary>推进一个固定步长(1/60 秒)。联机暂停=服务器不再推进。</summary>
        public void SimulateStep()
        {
            if (Phase != GamePhase.Playing) { PendingEvents.Clear(); return; }

            Tick++;
            Time += GameConfig.Tick;
            Level = (int)(Time / GameConfig.LevelDuration) + 1;
            Difficulty = Math.Min(1f, (Level - 1f) / GameConfig.DifficultyMaxLevelSpan);

            UpdateWaves();
            if (PlayerAlive) UpdatePlayer();
            else UpdateDeathSequence();
            UpdateEnemies();
            UpdateBullets();
            UpdatePowerups();
            ResolveCollisions();
        }

        // ------------------------- 波次生成 -------------------------

        private static float DelayAfterWave(int i)
        {
            int next = i + 1;
            if (next >= GameConfig.WaveCount) return 3.0f; // 一轮结束喘息 3 秒
            return GameConfig.WaveTable[next, 0];
        }

        private void UpdateWaves()
        {
            WaveTimer -= GameConfig.Tick;
            if (WaveTimer > 0f) return;
            SpawnWave(WaveIndex);
            WaveTimer = DelayAfterWave(WaveIndex);
            WaveIndex = (WaveIndex + 1) % GameConfig.WaveCount;
        }

        private void SpawnWave(int index)
        {
            int small = (int)GameConfig.WaveTable[index, 1];
            int medium = (int)GameConfig.WaveTable[index, 2];
            int large = (int)GameConfig.WaveTable[index, 3];
            int boss = (int)GameConfig.WaveTable[index, 4];
            for (int i = 0; i < small; i++) SpawnEnemy(EnemyKind.Small, i, small);
            for (int i = 0; i < medium; i++) SpawnEnemy(EnemyKind.Medium, i, medium);
            for (int i = 0; i < large; i++) SpawnEnemy(EnemyKind.Large, i, large);
            for (int i = 0; i < boss; i++) SpawnEnemy(EnemyKind.Boss, i, 1);
        }

        private void SpawnEnemy(EnemyKind kind, int slot, int totalInWave)
        {
            int k = (int)kind;
            float margin = GameConfig.EnemyHalfW[k] + 0.2f;
            var e = new Enemy
            {
                Id = _nextId++,
                Kind = kind,
                Hp = GameConfig.EnemyHp[k],
                MaxHp = GameConfig.EnemyHp[k],
                X = Rng.Range(margin, GameConfig.WorldWidth - margin), // 横向随机(边界内)
                Y = GameConfig.WorldHeight + GameConfig.EnemyHalfH[k] + slot * 1.35f, // 同波纵向错峰
                Speed = Math.Min(GameConfig.EnemyBaseSpeed[k] + GameConfig.EnemySpeedGain[k] * Difficulty,
                                 GameConfig.EnemyMaxSpeed[k]),
                FireInterval = GameConfig.EnemyFireIntervalBase[k] <= 0f
                    ? 0f
                    : Lerp(GameConfig.EnemyFireIntervalBase[k], GameConfig.EnemyFireIntervalMin[k], Difficulty),
                SwayPhase = Rng.NextFloat() * 6.2831853f,
                State = (byte)(kind == EnemyKind.Boss ? 1 : 0),
            };
            // 横向随机(保持边界内)
            e.X = Rng.Range(margin, GameConfig.WorldWidth - margin);
            if (totalInWave > 2 && kind == EnemyKind.Small)
            {
                // 小编队:部分排成两列纵队,更像原版
                if (slot % 2 == 1) e.X = Math.Min(GameConfig.WorldWidth - margin, e.X + 1.1f);
            }
            e.FireTimer = e.FireInterval <= 0f ? 0f : e.FireInterval * Rng.Range(0.4f, 1.0f);
            Enemies.Add(e);
        }

        private static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        // ------------------------- 玩家 -------------------------

        private void UpdatePlayer()
        {
            // 拖拽移动(速度上限钳制,保证手感稳定)
            if (_input.HasPointer)
            {
                HasTarget = true;
                PlayerTargetX = _input.X;
                PlayerTargetY = _input.Y;
            }
            if (HasTarget)
            {
                float dx = PlayerTargetX - PlayerX;
                float dy = PlayerTargetY - PlayerY;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float maxStep = GameConfig.PlayerMaxMoveSpeed * GameConfig.Tick;
                if (dist > 1e-5f)
                {
                    float step = dist < maxStep ? dist : maxStep;
                    PlayerX += dx / dist * step;
                    PlayerY += dy / dist * step;
                }
            }
            ClampPlayer();

            // 自动射击
            DoubleShotLeft -= GameConfig.Tick;
            FireTimer -= GameConfig.Tick;
            while (FireTimer <= 0f)
            {
                FirePlayerWeapon();
                FireTimer += GameConfig.FireInterval;
            }

            // 里程分(原版按飞行距离累计)
            float freq = Lerp(GameConfig.DistanceScoreFreqMin, GameConfig.DistanceScoreFreqMax, Difficulty);
            DistanceTimer += GameConfig.Tick * freq;
            while (DistanceTimer >= 1f)
            {
                DistanceTimer -= 1f;
                Score += GameConfig.ScorePerDistanceTick;
            }
        }

        private void FirePlayerWeapon()
        {
            float muzzleY = PlayerY + GameConfig.PlayerHalfH + 0.15f;
            if (DoubleShotLeft > 0f)
            {
                SpawnPlayerBullet(PlayerX - GameConfig.DoubleShotMuzzleOffsetX, muzzleY);
                SpawnPlayerBullet(PlayerX + GameConfig.DoubleShotMuzzleOffsetX, muzzleY);
            }
            else
            {
                SpawnPlayerBullet(PlayerX, muzzleY);
            }
            Emit(NetEvent.PlayerFired, PlayerX, muzzleY, DoubleShotLeft > 0f ? 1 : 0, 0);
        }

        private void SpawnPlayerBullet(float x, float y)
        {
            PlayerBullets.Add(new Bullet
            {
                Id = _nextId++,
                X = x, Y = y,
                VX = 0f, VY = GameConfig.PlayerBulletSpeed,
            });
        }

        private void ClampPlayer()
        {
            if (PlayerX < GameConfig.PlayerMinX) PlayerX = GameConfig.PlayerMinX;
            if (PlayerX > GameConfig.PlayerMaxX) PlayerX = GameConfig.PlayerMaxX;
            if (PlayerY < GameConfig.PlayerMinY) PlayerY = GameConfig.PlayerMinY;
            if (PlayerY > GameConfig.PlayerMaxY) PlayerY = GameConfig.PlayerMaxY;
        }

        private void UpdateDeathSequence()
        {
            DeadTimer -= GameConfig.Tick;
            PlayerY -= 3.0f * GameConfig.Tick; // 坠机
            if (DeadTimer <= 0f && !_gameOverEmitted)
            {
                _gameOverEmitted = true;
                Phase = GamePhase.GameOver;
                if (Score > HighScore) HighScore = Score;
                Emit(NetEvent.GameOver, PlayerX, PlayerY, Score, 0);
            }
        }

        // ------------------------- 敌机 -------------------------

        private void UpdateEnemies()
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (e.Flash > 0f) e.Flash -= GameConfig.Tick;

                if (e.Kind == EnemyKind.Boss) UpdateBoss(ref e);
                else
                {
                    e.Y -= e.Speed * GameConfig.Tick;
                    if (e.Kind == EnemyKind.Small)
                        e.X += (float)Math.Sin(Time * 1.3 + e.SwayPhase) * 0.3f * GameConfig.Tick;
                    if (e.FireInterval > 0f) UpdateEnemyFire(ref e);
                }
                Enemies[i] = e;
            }
            // 出屏回收
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                if (Enemies[i].Y < GameConfig.DespawnBelowY - GameConfig.EnemyHalfH[(int)Enemies[i].Kind])
                    Enemies.RemoveAt(i);
            }
        }

        private void UpdateBoss(ref Enemy e)
        {
            switch (e.State)
            {
                case 1: // 进场
                    e.Y -= e.Speed * 2.2f * GameConfig.Tick;
                    if (e.Y <= GameConfig.BossHoldY) { e.Y = GameConfig.BossHoldY; e.State = 2; e.HoldTimer = 0f; }
                    break;
                case 2: // 悬停:缓慢跟随玩家横向 + 扇形弹幕
                    e.HoldTimer += GameConfig.Tick;
                    float track = PlayerX - e.X;
                    if (track > 0.05f) e.X += Math.Min(0.6f, track) * GameConfig.Tick * 1.2f;
                    else if (track < -0.05f) e.X += Math.Max(-0.6f, track) * GameConfig.Tick * 1.2f;
                    UpdateEnemyFire(ref e);
                    if (e.HoldTimer >= GameConfig.BossHoldTime) e.State = 3;
                    break;
                case 3: // 俯冲离场
                    e.Y -= e.Speed * 2.6f * GameConfig.Tick;
                    e.X += (PlayerX > e.X ? 0.5f : -0.5f) * GameConfig.Tick;
                    break;
            }
        }

        private void UpdateEnemyFire(ref Enemy e)
        {
            if (e.FireInterval <= 0f) return;
            e.FireTimer -= GameConfig.Tick;
            if (e.FireTimer > 0f) return;
            if (e.Y > GameConfig.WorldHeight - 0.5f || e.Y < 1f) { e.FireTimer = e.FireInterval; return; }

            e.FireTimer = e.FireInterval * Rng.Range(0.9f, 1.1f);
            float s = Math.Min(GameConfig.EnemyBulletSpeedBase + GameConfig.EnemyBulletSpeedGain * Difficulty,
                               GameConfig.EnemyBulletSpeedMax);
            if (e.Kind == EnemyKind.Large) s *= GameConfig.EnemyBulletSpeedLargeMul;

            float mx = e.X, my = e.Y - GameConfig.EnemyHalfH[(int)e.Kind] - 0.1f;
            SpawnEnemyBullet(mx, my, 0f, -s);
            if (e.Kind == EnemyKind.Boss)
            {
                SpawnEnemyBullet(mx - 0.4f, my, -s * 0.45f, -s);
                SpawnEnemyBullet(mx + 0.4f, my, s * 0.45f, -s);
            }
            Emit(NetEvent.EnemyFired, mx, my, (int)e.Kind, e.Id);
        }

        private void SpawnEnemyBullet(float x, float y, float vx, float vy)
        {
            EnemyBullets.Add(new Bullet { Id = _nextId++, X = x, Y = y, VX = vx, VY = vy });
        }

        // ------------------------- 子弹 / 道具 -------------------------

        private void UpdateBullets()
        {
            for (int i = PlayerBullets.Count - 1; i >= 0; i--)
            {
                Bullet b = PlayerBullets[i];
                b.X += b.VX * GameConfig.Tick;
                b.Y += b.VY * GameConfig.Tick;
                if (b.Y > GameConfig.WorldHeight + 1f) PlayerBullets.RemoveAt(i);
                else PlayerBullets[i] = b;
            }
            for (int i = EnemyBullets.Count - 1; i >= 0; i--)
            {
                Bullet b = EnemyBullets[i];
                b.X += b.VX * GameConfig.Tick;
                b.Y += b.VY * GameConfig.Tick;
                if (b.Y < GameConfig.DespawnBelowY || b.X < -1f || b.X > GameConfig.WorldWidth + 1f)
                    EnemyBullets.RemoveAt(i);
                else EnemyBullets[i] = b;
            }
        }

        private void UpdatePowerups()
        {
            for (int i = Powerups.Count - 1; i >= 0; i--)
            {
                Powerup p = Powerups[i];
                p.Y -= GameConfig.PowerupSpeed * GameConfig.Tick;
                if (p.Y < -1f) Powerups.RemoveAt(i);
                else Powerups[i] = p;
            }
        }

        // ------------------------- 碰撞(AABB) -------------------------

        private static bool Overlap(float ax, float ay, float ahw, float ahh,
                                    float bx, float by, float bhw, float bhh)
        {
            return Math.Abs(ax - bx) < ahw + bhw && Math.Abs(ay - by) < ahh + bhh;
        }

        private void ResolveCollisions()
        {
            // 1) 玩家子弹 × 敌机
            for (int i = PlayerBullets.Count - 1; i >= 0; i--)
            {
                Bullet b = PlayerBullets[i];
                for (int j = 0; j < Enemies.Count; j++)
                {
                    Enemy e = Enemies[j];
                    int k = (int)e.Kind;
                    if (!Overlap(b.X, b.Y, GameConfig.PlayerBulletHalfW, GameConfig.PlayerBulletHalfH,
                                 e.X, e.Y, GameConfig.EnemyHalfW[k], GameConfig.EnemyHalfH[k]))
                        continue;

                    PlayerBullets.RemoveAt(i);
                    e.Hp -= GameConfig.PlayerBulletDamage;
                    e.Flash = GameConfig.EnemyHitFlashTime;
                    if (e.Hp <= 0)
                    {
                        Enemies.RemoveAt(j);
                        KillEnemy(e, true);
                    }
                    else Enemies[j] = e;
                    break;
                }
            }

            if (!PlayerAlive) return;

            // 2) 敌弹 × 玩家
            for (int i = EnemyBullets.Count - 1; i >= 0; i--)
            {
                Bullet b = EnemyBullets[i];
                if (Overlap(b.X, b.Y, GameConfig.EnemyBulletHalfW, GameConfig.EnemyBulletHalfH,
                            PlayerX, PlayerY, GameConfig.PlayerHalfW * 0.85f, GameConfig.PlayerHalfH * 0.85f))
                {
                    EnemyBullets.RemoveAt(i);
                    DamagePlayer();
                    if (!PlayerAlive) return;
                }
            }

            // 3) 敌机撞击 × 玩家
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                Enemy e = Enemies[i];
                int k = (int)e.Kind;
                if (!Overlap(e.X, e.Y, GameConfig.EnemyHalfW[k] * 0.8f, GameConfig.EnemyHalfH[k] * 0.8f,
                             PlayerX, PlayerY, GameConfig.PlayerHalfW * 0.8f, GameConfig.PlayerHalfH * 0.8f))
                    continue;

                DamagePlayer();
                e.Hp -= 2; // 撞击对敌机也有损耗(原版小机直接同归于尽)
                if (e.Hp <= 0) { Enemies.RemoveAt(i); KillEnemy(e, false); }
                else Enemies[i] = e;
                if (!PlayerAlive) return;
            }

            // 4) 道具拾取
            for (int i = Powerups.Count - 1; i >= 0; i--)
            {
                Powerup p = Powerups[i];
                if (!Overlap(p.X, p.Y, GameConfig.PowerupHalf, GameConfig.PowerupHalf,
                             PlayerX, PlayerY, GameConfig.PlayerHalfW + 0.15f, GameConfig.PlayerHalfH + 0.15f))
                    continue;
                Powerups.RemoveAt(i);
                ApplyPowerup(p);
            }
        }

        private void KillEnemy(Enemy e, bool scored)
        {
            int k = (int)e.Kind;
            if (scored)
            {
                Score += GameConfig.EnemyScore[k];
                Kills++;
            }
            Emit(e.Kind == EnemyKind.Boss ? NetEvent.BossKilled : NetEvent.EnemyKilled,
                 e.X, e.Y, GameConfig.EnemyScore[k], e.Id);
            if (scored) RollDrops(e);
        }

        private void RollDrops(Enemy e)
        {
            int col = e.Kind == EnemyKind.Medium ? 0 : e.Kind == EnemyKind.Large ? 1 : e.Kind == EnemyKind.Boss ? 2 : -1;
            if (col < 0) return; // 小型机不掉
            for (int row = 0; row < 3; row++)
            {
                float chance = GameConfig.DropChance[row, col];
                if (chance > 0f && Rng.Chance(chance))
                {
                    Powerups.Add(new Powerup
                    {
                        Id = _nextId++,
                        Kind = (PowerupKind)row,
                        X = Math.Min(Math.Max(e.X, 0.6f), GameConfig.WorldWidth - 0.6f),
                        Y = e.Y,
                    });
                }
            }
        }

        private void ApplyPowerup(Powerup p)
        {
            switch (p.Kind)
            {
                case PowerupKind.DoubleShot:
                    DoubleShotLeft = GameConfig.DoubleShotDuration;
                    break;
                case PowerupKind.Bomb:
                    DetonateBomb();
                    break;
                case PowerupKind.Life:
                    if (Lives < GameConfig.PlayerMaxLives) Lives++;
                    break;
            }
            Emit(NetEvent.PowerupPicked, p.X, p.Y, (int)p.Kind, p.Id);
        }

        private void DetonateBomb()
        {
            // 清屏:非 BOSS 全灭并计分,BOSS 受重创;清空敌弹。
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                Enemy e = Enemies[i];
                if (e.Kind == EnemyKind.Boss)
                {
                    e.Hp -= (int)GameConfig.BombDamageToBoss;
                    e.Flash = 0.2f;
                    if (e.Hp <= 0) { Enemies.RemoveAt(i); KillEnemy(e, true); }
                    else Enemies[i] = e;
                }
                else
                {
                    Enemies.RemoveAt(i);
                    KillEnemy(e, true);
                }
            }
            EnemyBullets.Clear();
            Emit(NetEvent.BombBlast, PlayerX, PlayerY, 0, 0);
        }

        private void DamagePlayer()
        {
            if (!PlayerAlive) return;
            Lives--;
            if (Lives <= 0)
            {
                Lives = 0;
                PlayerAlive = false;
                DeadTimer = GameConfig.PlayerDeathDuration;
                Emit(NetEvent.PlayerDead, PlayerX, PlayerY, 0, 0);
            }
            else
            {
                Emit(NetEvent.PlayerHit, PlayerX, PlayerY, Lives, 0);
            }
        }

        private void Emit(NetEvent type, float x, float y, int intParam, int entityId)
        {
            PendingEvents.Add(new GameEvent { Type = type, X = x, Y = y, IntParam = intParam, EntityId = entityId });
        }
    }
}
