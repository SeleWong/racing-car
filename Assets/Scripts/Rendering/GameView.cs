// ---------------------------------------------------------------------------
// GameView —— 表现层总控:
//   · 单机模式:以 60Hz 固定步进驱动本地 GameCore(时间累加器),渲染核心状态;
//   · 联机模式:每帧 Pump 会话、按 20Hz 快照做 120ms 延迟插值渲染,输入回传;
//   · 事件 → 一次性表现(爆炸/飘分/闪屏);实体视图全部走对象池。
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using PlaneWar.Core;
using PlaneWar.Gameplay;
using PlaneWar.Net;
using PlaneWar.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PlaneWar.Rendering
{
    public sealed class GameView : MonoBehaviour
    {
        private const float PixelsPerUnit = EntityView.PixelsPerUnit;
        private const float InterpDelay = 0.12f;   // 联机插值延迟(秒)

        [Header("运行模式(由 GameBootstrap 写入)")]
        public bool OnlineMode;
        public string ServerUrl = "ws://127.0.0.1:8080/";
        public uint Seed = 20260927;

        [Header("引用")]
        public Camera WorldCamera;
        public DragInput Drag;
        public UIManager Ui;
        public RectTransform EntityRoot;          // 左下角锚点,900×1600
        public Image FlashOverlay;                // 全屏受击/炸弹闪屏

        // ------------------------- 单机状态 -------------------------
        private GameCore _core;
        private bool _paused;
        private float _stepAcc;

        // ------------------------- 联机状态 -------------------------
        private RemoteSession _session;
        private float _sendAcc;

        // ------------------------- 视图与对象池 -------------------------
        private PlayerView _player;
        private readonly List<EnemyView> _enemyViews = new List<EnemyView>();
        private readonly List<EntityView> _pBulletViews = new List<EntityView>();
        private readonly List<EntityView> _eBulletViews = new List<EntityView>();
        private readonly List<PowerupView> _powerViews = new List<PowerupView>();
        private ExplosionPool _explosions;
        private ScorePopupPool _popups;
        private BackgroundScroller _bg;

        private GamePhase _shownPhase = GamePhase.Ready;
        private float _flashTimer;
        private float _flashMax;
        private float _flashAlpha;
        private Color _flashColor;

        // ========================================================= 初始化

        public void InitLocal(uint seed)
        {
            OnlineMode = false;
            if (Ui != null) Ui.ShowConnecting(false, null); // 兜底:确保连接面板关闭
            int high = PlayerPrefs.GetInt("highscore", 0);
            _core = new GameCore(seed, high);
            _paused = false;
            SetPhaseUi(GamePhase.Ready);
            Ui.UpdateMenuHigh(high);
        }

        public void InitOnline(string url)
        {
            OnlineMode = true;
            ServerUrl = url;
            _session = new RemoteSession(new WebSocketClient());
            _session.Start(url);
            SetPhaseUi(GamePhase.Ready); // 连接面板由 Update 控制
            Ui.ShowConnecting(true, null);
        }

        private void OnDestroy()
        {
            if (_session != null) _session.Stop();
        }

        // ========================================================= 帧循环

        private void Update()
        {
            if (!SpriteFactory.Ready || _bg == null)
            {
                if (_bg == null) TryBuildSceneDressing();
                if (_bg == null) return;
            }

            bool worldActive = _shownPhase == GamePhase.Playing && !_paused;
            _bg.Tick(Time.deltaTime, worldActive);
            UpdateFlash(Time.deltaTime);

            if (OnlineMode) UpdateOnline();
            else UpdateLocal();
        }

        private void TryBuildSceneDressing()
        {
            if (EntityRoot == null || !SpriteFactory.Ready) return;
            _bg = BackgroundScroller.Build(EntityRoot, SpriteFactory.Background);
            _explosions = ExplosionPool.Build(EntityRoot);
            _popups = ScorePopupPool.Build(EntityRoot);
            _player = BuildPlayerView();
        }

        // ========================================================= 单机

        private void UpdateLocal()
        {
            if (_core == null) return;

            if (_core.Phase == GamePhase.Playing && !_paused)
            {
                _core.SetInput(new PlayerInput
                {
                    HasPointer = Drag != null && Drag.IsDragging,
                    X = Drag != null ? Drag.TargetWorld.x : 0f,
                    Y = Drag != null ? Drag.TargetWorld.y : 0f,
                });

                _stepAcc += Time.deltaTime;
                if (_stepAcc > 0.25f) _stepAcc = 0.25f; // 防切后台大步进
                int steps = 0;
                while (_stepAcc >= GameConfig.Tick && steps < 16)
                {
                    _core.SimulateStep();
                    DrainLocalEvents();
                    _stepAcc -= GameConfig.Tick;
                    steps++;
                }
            }

            RenderFromCore();
            SyncLocalPhase();
        }

        private void DrainLocalEvents()
        {
            var evs = _core.PendingEvents;
            for (int i = 0; i < evs.Count; i++) HandleGameEvent(evs[i]);
            evs.Clear();
        }

        private void SyncLocalPhase()
        {
            if (_core.Phase != _shownPhase)
            {
                if (_core.Phase == GamePhase.GameOver)
                {
                    PlayerPrefs.SetInt("highscore", _core.HighScore);
                    PlayerPrefs.Save();
                    Ui.ShowGameOver(_core.Score, _core.HighScore, _core.Kills,
                                    _core.Time, _core.Score >= _core.HighScore && _core.Score > 0);
                }
                SetPhaseUi(_core.Phase);
            }
            if (_shownPhase == GamePhase.Playing)
            {
                Ui.UpdateHud(_core.Score, _core.Lives,
                             Mathf.Clamp01(_core.DoubleShotLeft / GameConfig.DoubleShotDuration),
                             _paused);
            }
        }

        // ========================================================= 联机

        private void UpdateOnline()
        {
            if (_session == null) return;
            _session.Pump();

            // 断线:回主菜单并提示
            if (_session.State == SessionState.Disconnected)
            {
                _session.Stop();
                _session = null;
                OnlineMode = false;
                InitLocal(Seed);
                Ui.ShowConnecting(false, "Connection to server lost.");
                return;
            }

            // 连接中面板状态
            if (_session.State == SessionState.Connecting || _session.Latest == null)
            {
                Ui.ShowConnecting(_session.State != SessionState.Idle, null);
                return;
            }
            Ui.ShowConnecting(false, null);

            // 输入回传(按固定节拍)
            _sendAcc += Time.deltaTime;
            while (_sendAcc >= GameConfig.Tick)
            {
                _sendAcc -= GameConfig.Tick;
                _session.SendInput(Drag != null && Drag.IsDragging,
                                   Drag != null ? Drag.TargetWorld.x : 0f,
                                   Drag != null ? Drag.TargetWorld.y : 0f);
            }

            // 事件(一次性表现)
            EventMsg ev;
            while (_session.PollEvent(out ev))
            {
                HandleGameEvent(new GameEvent
                {
                    Type = ev.Type, X = ev.X, Y = ev.Y,
                    IntParam = ev.IntParam, EntityId = ev.EntityId,
                });
            }

            RenderFromSnapshots();
            SyncOnlinePhase();
        }

        private void SyncOnlinePhase()
        {
            var latest = _session.Latest;
            if (latest == null) return;
            var phase = (GamePhase)latest.Phase;

            if (phase == GamePhase.GameOver && _shownPhase != GamePhase.GameOver)
            {
                Ui.ShowGameOver(latest.Score, latest.HighScore, latest.Kills,
                                latest.Time, latest.Score >= latest.HighScore && latest.Score > 0);
            }
            if (phase != _shownPhase) SetPhaseUi(phase);

            if (phase == GamePhase.Playing)
            {
                Ui.UpdateHud(latest.Score, latest.Lives,
                             Mathf.Clamp01(latest.DoubleShotLeft / GameConfig.DoubleShotDuration),
                             false);
            }
        }

        /// <summary>取 renderAt 时刻前后两个快照并插值(不足则钳制)。</summary>
        private bool PickSnapshots(out SnapshotData a, out SnapshotData b, out float t)
        {
            var snaps = _session.Snapshots;
            a = b = null; t = 0f;
            if (snaps.Count == 0) return false;
            float renderAt = Time.realtimeSinceStartup - InterpDelay;
            if (renderAt <= snaps[0].ReceivedAt || snaps.Count == 1)
            {
                a = b = snaps[snaps.Count - 1].Data; t = 0f;
                return true;
            }
            for (int i = snaps.Count - 2; i >= 0; i--)
            {
                if (snaps[i].ReceivedAt <= renderAt)
                {
                    a = snaps[i].Data;
                    b = snaps[i + 1].Data;
                    float span = snaps[i + 1].ReceivedAt - snaps[i].ReceivedAt;
                    t = span <= 1e-6f ? 1f : Mathf.Clamp01((renderAt - snaps[i].ReceivedAt) / span);
                    return true;
                }
            }
            a = b = snaps[snaps.Count - 1].Data;
            return true;
        }

        private void RenderFromSnapshots()
        {
            SnapshotData a, b;
            float t;
            if (!PickSnapshots(out a, out b, out t)) return;

            float px = Mathf.Lerp(a.PlayerX, b.PlayerX, t);
            float py = Mathf.Lerp(a.PlayerY, b.PlayerY, t);
            bool alive = a.PlayerAlive != 0;
            RenderPlayer(px, py, alive, a.DeadTimer);

            RenderEnemiesInterp(a, b, t);
            RenderBulletsInterp(a.PlayerBullets, b.PlayerBullets, t, _pBulletViews, true);
            RenderBulletsInterp(a.EnemyBullets, b.EnemyBullets, t, _eBulletViews, false);
            RenderPowerupsInterp(a, b, t);
        }

        // ========================================================= 事件表现

        private void HandleGameEvent(GameEvent ev)
        {
            switch (ev.Type)
            {
                case NetEvent.EnemyKilled:
                    _explosions.Play(ev.X, ev.Y, ExplosionSizeFor(ev.IntParam), TintForScore(ev.IntParam));
                    _popups.Play(ev.X, ev.Y, "+" + ev.IntParam, new Color(1f, 0.9f, 0.5f));
                    break;
                case NetEvent.BossKilled:
                    _explosions.Play(ev.X, ev.Y, 2.6f, new Color(1f, 0.55f, 0.75f));
                    _explosions.Play(ev.X - 1f, ev.Y + 0.6f, 1.6f, new Color(1f, 0.8f, 0.4f));
                    _explosions.Play(ev.X + 1f, ev.Y - 0.5f, 1.6f, new Color(0.6f, 0.8f, 1f));
                    _popups.Play(ev.X, ev.Y, "+" + ev.IntParam, new Color(1f, 0.5f, 0.8f));
                    break;
                case NetEvent.PlayerHit:
                    TriggerFlash(new Color(1f, 0.15f, 0.1f), 0.35f, 0.15f);
                    _explosions.Play(ev.X, ev.Y, 1.0f, new Color(1f, 0.6f, 0.3f));
                    break;
                case NetEvent.PlayerDead:
                    TriggerFlash(new Color(1f, 0.2f, 0.15f), 0.5f, 0.25f);
                    _explosions.Play(ev.X, ev.Y, 2.2f, new Color(1f, 0.7f, 0.35f));
                    break;
                case NetEvent.PowerupPicked:
                    string label = ev.IntParam == 0 ? "双倍火力!" : ev.IntParam == 1 ? "炸弹!" : "生命 +1";
                    var tint = ev.IntParam == 0 ? new Color(0.4f, 0.85f, 1f)
                             : ev.IntParam == 1 ? new Color(1f, 0.75f, 0.3f)
                             : new Color(0.5f, 1f, 0.6f);
                    _popups.Play(ev.X, ev.Y + 0.8f, label, tint);
                    break;
                case NetEvent.BombBlast:
                    TriggerFlash(new Color(1f, 1f, 1f), 0.55f, 0.3f);
                    break;
            }
        }

        private static float ExplosionSizeFor(int score)
        {
            if (score >= 100) return 2.4f;
            if (score >= 50) return 1.8f;
            if (score >= 20) return 1.2f;
            return 0.8f;
        }

        private static Color TintForScore(int score)
        {
            if (score >= 50) return new Color(0.75f, 0.85f, 1f);
            if (score >= 20) return new Color(1f, 0.75f, 0.45f);
            return new Color(1f, 0.55f, 0.35f);
        }

        private void TriggerFlash(Color color, float alpha, float duration)
        {
            _flashColor = color;
            _flashAlpha = alpha;
            _flashTimer = duration;
            _flashMax = duration;
            FlashOverlay.color = new Color(color.r, color.g, color.b, alpha);
        }

        private void UpdateFlash(float dt)
        {
            if (FlashOverlay == null) return;
            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                float k = Mathf.Clamp01(_flashTimer / Mathf.Max(1e-4f, _flashMax));
                FlashOverlay.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flashAlpha * k);
                if (_flashTimer <= 0f) FlashOverlay.color = new Color(0, 0, 0, 0);
            }
        }

        // ========================================================= 渲染:从核心

        private void RenderFromCore()
        {
            RenderPlayer(_core.PlayerX, _core.PlayerY, _core.PlayerAlive, _core.DeadTimer);

            // 敌机
            for (int i = 0; i < _enemyViews.Count; i++) _enemyViews[i].InUse = false;
            for (int i = 0; i < _core.Enemies.Count; i++)
            {
                Enemy e = _core.Enemies[i];
                EnemyView v = AcquireEnemyView(e.Id);
                if (v.Kind != e.Kind) v.Setup(e.Kind, EnemySprite(e.Kind),
                                              GameConfig.EnemyHalfW[(int)e.Kind] * 2f,
                                              GameConfig.EnemyHalfH[(int)e.Kind] * 2f);
                v.SetWorldPos(e.X, e.Y);
                v.SetHp(e.Hp / (float)e.MaxHp);
                if (e.Flash > 0f) v.SetFlash(e.Flash);
            }
            SweepPool(_enemyViews);

            RenderBulletsFrom(_core.PlayerBullets, _pBulletViews, true);
            RenderBulletsFrom(_core.EnemyBullets, _eBulletViews, false);

            for (int i = 0; i < _powerViews.Count; i++) _powerViews[i].InUse = false;
            for (int i = 0; i < _core.Powerups.Count; i++)
            {
                Powerup p = _core.Powerups[i];
                PowerupView v = AcquirePowerupView(p.Id);
                if (v.Img.sprite != PowerupSprite(p.Kind))
                    v.SetSprite(PowerupSprite(p.Kind), GameConfig.PowerupHalf * 2f, GameConfig.PowerupHalf * 2f);
                v.SetWorldPos(p.X, p.Y);
            }
            SweepPool(_powerViews);
        }

        private void RenderPlayer(float x, float y, bool alive, float deadTimer)
        {
            if (_player == null) return;
            if (!alive && deadTimer <= 0f && _shownPhase == GamePhase.GameOver)
            {
                _player.gameObject.SetActive(false);
                return;
            }
            if (!_player.gameObject.activeSelf) _player.gameObject.SetActive(true);
            _player.SetWorldPos(x, y + GameConfig.PlayerVisualYOffset);
            if (!alive)
            {
                float progress = 1f - Mathf.Clamp01(deadTimer / GameConfig.PlayerDeathDuration);
                _player.PlayDeath(progress);
            }
            else _player.PlayNormal();
        }

        // ========================================================= 渲染:插值(联机)

        private void RenderEnemiesInterp(SnapshotData a, SnapshotData b, float t)
        {
            for (int i = 0; i < _enemyViews.Count; i++) _enemyViews[i].InUse = false;
            for (int i = 0; i < b.Enemies.Count; i++)
            {
                var cur = b.Enemies[i];
                float x = cur.X, y = cur.Y;
                for (int j = 0; j < a.Enemies.Count; j++)
                {
                    if (a.Enemies[j].Id == cur.Id)
                    {
                        x = Mathf.Lerp(a.Enemies[j].X, cur.X, t);
                        y = Mathf.Lerp(a.Enemies[j].Y, cur.Y, t);
                        break;
                    }
                }
                var kind = (EnemyKind)cur.Kind;
                EnemyView v = AcquireEnemyView(cur.Id);
                if (v.Kind != kind)
                    v.Setup(kind, EnemySprite(kind),
                            GameConfig.EnemyHalfW[cur.Kind] * 2f, GameConfig.EnemyHalfH[cur.Kind] * 2f);
                v.SetWorldPos(x, y);
                v.SetHp(cur.Hp / (float)Mathf.Max(1, cur.MaxHp));
            }
            SweepPool(_enemyViews);
        }

        private void RenderBulletsInterp(List<SnapshotData.BulletSnap> from,
                                         List<SnapshotData.BulletSnap> to,
                                         float t, List<EntityView> pool, bool playerBullet)
        {
            for (int i = 0; i < pool.Count; i++) pool[i].InUse = false;
            for (int i = 0; i < to.Count; i++)
            {
                var cur = to[i];
                float x = cur.X, y = cur.Y;
                for (int j = 0; j < from.Count; j++)
                {
                    if (from[j].Id == cur.Id)
                    {
                        x = Mathf.Lerp(from[j].X, cur.X, t);
                        y = Mathf.Lerp(from[j].Y, cur.Y, t);
                        break;
                    }
                }
                EntityView v = AcquireBulletView(pool, cur.Id, playerBullet);
                v.SetWorldPos(x, y);
            }
            SweepPool(pool);
        }

        private void RenderPowerupsInterp(SnapshotData a, SnapshotData b, float t)
        {
            for (int i = 0; i < _powerViews.Count; i++) _powerViews[i].InUse = false;
            for (int i = 0; i < b.Powerups.Count; i++)
            {
                var cur = b.Powerups[i];
                float x = cur.X, y = cur.Y;
                for (int j = 0; j < a.Powerups.Count; j++)
                {
                    if (a.Powerups[j].Id == cur.Id)
                    {
                        x = Mathf.Lerp(a.Powerups[j].X, cur.X, t);
                        y = Mathf.Lerp(a.Powerups[j].Y, cur.Y, t);
                        break;
                    }
                }
                var kind = (PowerupKind)cur.Kind;
                PowerupView v = AcquirePowerupView(cur.Id);
                if (v.Img.sprite != PowerupSprite(kind))
                    v.SetSprite(PowerupSprite(kind), GameConfig.PowerupHalf * 2f, GameConfig.PowerupHalf * 2f);
                v.SetWorldPos(x, y);
            }
            SweepPool(_powerViews);
        }

        private void RenderBulletsFrom(List<Bullet> bullets, List<EntityView> pool, bool playerBullet)
        {
            for (int i = 0; i < pool.Count; i++) pool[i].InUse = false;
            for (int i = 0; i < bullets.Count; i++)
            {
                Bullet b = bullets[i];
                EntityView v = AcquireBulletView(pool, b.Id, playerBullet);
                v.SetWorldPos(b.X, b.Y);
            }
            SweepPool(pool);
        }

        /// <summary>回收本帧未被认领但仍激活的视图。</summary>
        private static void SweepPool<T>(List<T> pool) where T : EntityView
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].InUse && pool[i].gameObject.activeSelf) pool[i].OnRecycle();
            }
        }

        // ========================================================= 视图工厂

        private PlayerView BuildPlayerView()
        {
            var go = new GameObject("Player", typeof(RectTransform));
            go.transform.SetParent(EntityRoot, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var v = go.AddComponent<PlayerView>();
            v.Rt = (RectTransform)go.transform;
            v.Img = img;
            v.SetSprite(SpriteFactory.Player, GameConfig.PlayerHalfW * 2f + 0.35f,
                        GameConfig.PlayerHalfH * 2f + 0.55f);
            return v;
        }

        private EnemyView AcquireEnemyView(int id)
        {
            for (int i = 0; i < _enemyViews.Count; i++)
            {
                if (_enemyViews[i].EntityId == id && _enemyViews[i].InUse)
                {
                    return _enemyViews[i];
                }
            }
            EnemyView free = null;
            for (int i = 0; i < _enemyViews.Count; i++)
            {
                if (!_enemyViews[i].InUse) { free = _enemyViews[i]; break; }
            }
            if (free == null)
            {
                free = BuildEnemyView();
                _enemyViews.Add(free);
            }
            free.OnSpawn(id);
            return free;
        }

        private EnemyView BuildEnemyView()
        {
            var go = new GameObject("Enemy", typeof(RectTransform));
            go.transform.SetParent(EntityRoot, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var v = go.AddComponent<EnemyView>();
            v.Rt = (RectTransform)go.transform;
            v.Img = img;

            // 受击白闪层
            var flashGo = new GameObject("Flash", typeof(RectTransform));
            flashGo.transform.SetParent(go.transform, false);
            var flashImg = flashGo.AddComponent<Image>();
            flashImg.raycastTarget = false;
            flashImg.sprite = SpriteFactory.White;
            flashImg.color = new Color(1, 1, 1, 0);
            var frt = (RectTransform)flashGo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            v.FlashOverlay = flashImg;

            // 血条(大型机/BOSS 可见)
            var barRoot = new GameObject("HpBar", typeof(RectTransform));
            barRoot.transform.SetParent(go.transform, false);
            var brt = (RectTransform)barRoot.transform;
            brt.anchorMin = new Vector2(0.5f, 1f); brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(90f, 7f);
            brt.anchoredPosition = new Vector2(0f, 8f);
            var bgImg = barRoot.AddComponent<Image>();
            bgImg.raycastTarget = false;
            bgImg.sprite = SpriteFactory.White;
            bgImg.color = new Color(0f, 0f, 0f, 0.55f);
            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(barRoot.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.raycastTarget = false;
            fillImg.sprite = SpriteFactory.White;
            fillImg.color = new Color(0.95f, 0.35f, 0.3f);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = new Vector2(1f, 1f); fillRt.offsetMax = new Vector2(-1f, -1f);
            v.HpBarFill = fillRt;
            barRoot.SetActive(false);
            return v;
        }

        private EntityView AcquireBulletView(List<EntityView> pool, int id, bool playerBullet)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].EntityId == id && pool[i].InUse) return pool[i];
            }
            EntityView free = null;
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].InUse) { free = pool[i]; break; }
            }
            if (free == null)
            {
                var go = new GameObject(playerBullet ? "PBullet" : "EBullet", typeof(RectTransform));
                go.transform.SetParent(EntityRoot, false);
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                free = go.AddComponent<EntityView>();
                free.Rt = (RectTransform)go.transform;
                free.Img = img;
                free.SetSprite(playerBullet ? SpriteFactory.PlayerBullet : SpriteFactory.EnemyBullet,
                               playerBullet ? 0.42f : 0.55f,
                               playerBullet ? 1.15f : 0.55f);
                pool.Add(free);
            }
            free.OnSpawn(id);
            return free;
        }

        private PowerupView AcquirePowerupView(int id)
        {
            for (int i = 0; i < _powerViews.Count; i++)
            {
                if (_powerViews[i].EntityId == id && _powerViews[i].InUse) return _powerViews[i];
            }
            PowerupView free = null;
            for (int i = 0; i < _powerViews.Count; i++)
            {
                if (!_powerViews[i].InUse) { free = _powerViews[i]; break; }
            }
            if (free == null)
            {
                var go = new GameObject("Powerup", typeof(RectTransform));
                go.transform.SetParent(EntityRoot, false);
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                free = go.AddComponent<PowerupView>();
                free.Rt = (RectTransform)go.transform;
                free.Img = img;
                _powerViews.Add(free);
            }
            free.OnSpawn(id);
            return free;
        }

        private static Sprite EnemySprite(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Small: return SpriteFactory.EnemySmall;
                case EnemyKind.Medium: return SpriteFactory.EnemyMedium;
                case EnemyKind.Large: return SpriteFactory.EnemyLarge;
                default: return SpriteFactory.EnemyBoss;
            }
        }

        private static Sprite PowerupSprite(PowerupKind kind)
        {
            switch (kind)
            {
                case PowerupKind.DoubleShot: return SpriteFactory.PowerDouble;
                case PowerupKind.Bomb: return SpriteFactory.PowerBomb;
                default: return SpriteFactory.PowerLife;
            }
        }

        // ========================================================= 阶段 / 用户操作

        private void SetPhaseUi(GamePhase phase)
        {
            _shownPhase = phase;
            Ui.SetPhase(phase, OnlineMode);
            if (phase == GamePhase.Playing && _bg != null) _bg.ResetScroll();
        }

        public void BeginNewGame()
        {
            if (OnlineMode)
            {
                if (_session != null) _session.RequestReset();
                return;
            }
            _core.Restart();
            _core.BeginGame();
            _stepAcc = 0f;
            _paused = false;
        }

        public void TogglePause()
        {
            if (OnlineMode || _shownPhase != GamePhase.Playing) return;
            _paused = !_paused;
            Ui.UpdateHud(_core.Score, _core.Lives,
                         Mathf.Clamp01(_core.DoubleShotLeft / GameConfig.DoubleShotDuration), _paused);
            Ui.ShowPauseOverlay(_paused);
        }

        public void Resume()
        {
            if (_paused) TogglePause();
        }

        public void BackToMenu()
        {
            _paused = false;
            if (OnlineMode)
            {
                if (_session != null) { _session.Stop(); _session = null; }
                OnlineMode = false;
                InitLocal(Seed);
                return;
            }
            _core.Restart(); // 回 Ready,等下次开始
            SetPhaseUi(GamePhase.Ready);
            Ui.UpdateMenuHigh(_core.HighScore);
        }

        public void CancelOnline()
        {
            if (_session != null) { _session.Stop(); _session = null; }
            OnlineMode = false;
            InitLocal(Seed);
        }

        /// <summary>拖拽输入的基准点:玩家当前逻辑锚点(世界坐标)。</summary>
        public Vector2 GetPlayerAnchor()
        {
            if (!OnlineMode && _core != null) return new Vector2(_core.PlayerX, _core.PlayerY);
            if (_session != null && _session.Latest != null)
                return new Vector2(_session.Latest.PlayerX, _session.Latest.PlayerY);
            return new Vector2(GameConfig.WorldWidth * 0.5f, 2.4f);
        }
    }
}
