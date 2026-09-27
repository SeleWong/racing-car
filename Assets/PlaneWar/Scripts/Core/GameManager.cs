using UnityEngine;
using UnityEngine.EventSystems;

namespace PlaneWar
{
    /// <summary>
    /// 游戏入口 + 状态机。挂在场景任意物体上即可（或由 GameBootstrap 自动创建）。
    /// 负责组装各子系统，并驱动：主界面 → 游戏中 ⇄ 暂停 → 坠毁 → 结算。
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public const string BestScoreKey = "PlaneWar.BestScore";
        public const string MutedKey = "PlaneWar.Muted";

        public static GameManager Instance { get; private set; }

        [Tooltip("可选：直接拖入配置。为空时从 Resources/PlaneWarConfig 加载，再为空使用默认值")]
        public GameConfig config;

        public GameState State { get; private set; }
        public int Score { get; private set; }
        public int BestScore { get; private set; }
        public int Bombs { get; private set; }
        public int Lives { get; private set; }
        public int LevelIndex { get; private set; }
        public bool IsNewRecord { get; private set; }
        public bool ReviveUsed { get; private set; }
        public bool PlatformReady { get; private set; }

        public GameConfig Config { get { return config; } }
        public GameWorld World { get; private set; }
        public SpriteLibrary Sprites { get; private set; }
        public IPlatformService Platform { get; private set; }
        public InputRouter Input { get; private set; }
        public AudioManager Audio { get; private set; }
        public UIManager UI { get; private set; }
        public WorldBounds Bounds { get; private set; }

        public DifficultyLevel Level { get { return config.levels[Mathf.Clamp(LevelIndex, 0, config.levels.Length - 1)]; } }

        /// <summary>是否可以看广告复活。</summary>
        public bool CanRevive
        {
            get
            {
                return config.allowRevive && !ReviveUsed && Platform != null && Platform.SupportsRewardedAd
                       && (Application.isEditor || !string.IsNullOrEmpty(config.rewardedAdUnitId));
            }
        }

        private EnemySpawner _enemySpawner;
        private SupplySpawner _supplySpawner;
        private ScrollingBackground _background;
        private float _dyingTimer;
        private bool _waitingForAd;
        private float _adRequestTime;
        private bool _ownsConfig;
        private PerfOverlay _perf;

        private const float AdTimeout = 8f;

        private const float PlayerDyingDuration = 1.2f;

        // ------------------------------------------------------------------ 生命周期

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (config == null) config = GameConfig.Load();
            else config.Validate();
            _ownsConfig = config.IsRuntimeInstance;

            Platform = PlatformServices.Current;
            Platform.Hidden += OnPlatformHidden;

            Application.targetFrameRate = Platform.PreferredFrameRate(config.targetFrameRate);
            if (Application.isMobilePlatform) QualitySettings.vSyncCount = 0; // 由 targetFrameRate 控制，省电且稳定
            UnityEngine.Input.multiTouchEnabled = true;
            Application.lowMemory += OnLowMemory;

            BuildWorld();
            State = GameState.Home;

            // 启动期生成资源产生的临时垃圾（像素缓冲、音频采样）在加载界面期间一次性回收，
            // 避免在游戏过程中触发 GC 卡顿
            System.GC.Collect();
        }

        private void Start()
        {
            UI.ShowLoading(true);
            Platform.Init(OnPlatformReady);
        }

        private void OnPlatformReady()
        {
            PlatformReady = true;
            BestScore = Platform.LoadInt(BestScoreKey, 0);
            Audio.Muted = Platform.LoadInt(MutedKey, 0) == 1;
            UI.ShowLoading(false);
            SetState(GameState.Home, true);
        }

        private void OnDestroy()
        {
            Application.lowMemory -= OnLowMemory;
            if (Platform != null) Platform.Hidden -= OnPlatformHidden;
            if (World != null)
            {
                World.OnEnemyDestroyed = null;
                World.OnSupplyCollected = null;
                World.OnPlayerCollided = null;
            }
            // 释放运行时生成的纹理 / Sprite（原生内存，不会被 GC 回收）
            if (Sprites != null) Sprites.Dispose();
            if (_ownsConfig && config != null) Destroy(config);
            if (Instance == this) Instance = null;
        }

        /// <summary>系统内存告警（iOS / Android）：收缩对象池并回收。</summary>
        private void OnLowMemory()
        {
            if (World != null) World.TrimPools();
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }

        private void OnPlatformHidden()
        {
            if (State == GameState.Playing) Pause();
        }

        private void BuildWorld()
        {
            // 相机
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            if (FindObjectOfType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            Bounds = gameObject.AddComponent<WorldBounds>();
            Bounds.Init(config, cam);

            // UI 事件系统
            if (EventSystem.current == null && FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                es.transform.SetParent(transform, false);
            }

            Sprites = SpriteLibrary.Build(config);

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(transform, false);
            _background = bgGo.AddComponent<ScrollingBackground>();
            _background.Init(Sprites.Background, config.backgroundScrollSpeed);

            var worldGo = new GameObject("World");
            worldGo.transform.SetParent(transform, false);
            World = worldGo.AddComponent<GameWorld>();
            World.Init(config, Sprites);
            World.OnEnemyDestroyed = HandleEnemyDestroyed;
            World.OnSupplyCollected = HandleSupplyCollected;
            World.OnPlayerCollided = HandlePlayerCollided;

            _enemySpawner = new EnemySpawner(config, World);
            _supplySpawner = new SupplySpawner(config, World);
            Input = InputRouter.CreateDefault();

            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(transform, false);
            Audio = audioGo.AddComponent<AudioManager>();
            Audio.Init(config);

            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform, false);
            UI = uiGo.AddComponent<UIManager>();
            UI.Init(this, cam);

            _perf = uiGo.AddComponent<PerfOverlay>();
            _perf.Init(this, UI.Factory, UI.SafeRoot, config.showPerfStats);
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f); // 防止卡顿后穿模
            Input.Tick(Bounds.GameCamera, config);
            if (UnityEngine.Input.GetKeyDown(KeyCode.F1)) _perf.Toggle();
            if (_waitingForAd && Time.unscaledTime - _adRequestTime > AdTimeout) _waitingForAd = false; // 广告无回调兜底

            switch (State)
            {
                case GameState.Home:
                case GameState.GameOver:
                    _background.Tick(dt * 0.5f);
                    World.TickEffects(dt);
                    break;

                case GameState.Paused:
                    if (Input.PausePressed) Resume();
                    break;

                case GameState.Playing:
                    if (Input.PausePressed) { Pause(); break; }
                    if (Input.BombPressed) UseBomb();

                    _background.Tick(dt);
                    World.Player.Tick(dt, Input.MoveDelta);
                    _enemySpawner.Tick(dt, Level);
                    _supplySpawner.Tick(dt);
                    World.TickEntities(dt);
                    World.CheckCollisions();
                    break;

                case GameState.PlayerDying:
                    _background.Tick(dt);
                    World.TickEntities(dt);
                    World.CheckCollisions(); // 仅子弹仍可击中敌机
                    _dyingTimer -= dt;
                    if (_dyingTimer <= 0f) OnDyingFinished();
                    break;
            }
        }

        // 切到后台 / 失去焦点自动暂停（手机来电、小游戏切后台、PC 切窗口）
        private void OnApplicationPause(bool paused)
        {
            if (paused && State == GameState.Playing) Pause();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && !Application.isEditor && State == GameState.Playing) Pause();
        }

        // ------------------------------------------------------------------ 状态切换（UI 调用）

        public void StartGame()
        {
            if (!PlatformReady) return;
            World.ClearAll();
            Score = 0;
            LevelIndex = 0;
            IsNewRecord = false;
            ReviveUsed = false;
            Bombs = Mathf.Clamp(config.startBombs, 0, config.maxBombs);
            Lives = config.playerLives;
            _enemySpawner.Reset();
            _supplySpawner.Reset();
            Input.Reset();
            World.Player.Spawn(config.spawnInvincible, false);

            GameEvents.RaiseScoreChanged(Score);
            GameEvents.RaiseBombCountChanged(Bombs);
            GameEvents.RaiseLivesChanged(Lives);
            GameEvents.RaiseLevelChanged(LevelIndex);
            Platform.OnGameStart();
            SetState(GameState.Playing);
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            Input.Reset();
            SetState(GameState.Playing);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) Pause();
            else if (State == GameState.Paused) Resume();
        }

        public void Restart() { StartGame(); }

        public void GoHome()
        {
            World.ClearAll();
            World.Player.Hide();
            SetState(GameState.Home);
        }

        public void UseBomb()
        {
            if (State != GameState.Playing || Bombs <= 0) return;
            Bombs--;
            GameEvents.RaiseBombCountChanged(Bombs);
            World.DestroyAllOnScreen();
            GameEvents.RaiseBombUsed();
            Platform.Vibrate(VibrateStrength.Heavy);
        }

        /// <summary>看激励视频复活（每局一次）。</summary>
        public void RequestRevive()
        {
            if (State != GameState.GameOver || !CanRevive || _waitingForAd) return;
            _waitingForAd = true;
            _adRequestTime = Time.unscaledTime;
            Platform.ShowRewardedAd(config.rewardedAdUnitId, success =>
            {
                _waitingForAd = false;
                if (!success || State != GameState.GameOver) return;
                ReviveUsed = true;
                Lives = 1;
                GameEvents.RaiseLivesChanged(Lives);
                World.KillAllEnemiesSilently();
                World.Player.Spawn(config.reviveInvincible, false);
                Input.Reset();
                SetState(GameState.Playing);
            });
        }

        public void Share()
        {
            if (Platform.SupportsShare) Platform.Share(string.Format(config.shareTitle, Score), Score);
        }

        public void ToggleMute()
        {
            Audio.Muted = !Audio.Muted;
            Platform.SaveInt(MutedKey, Audio.Muted ? 1 : 0);
        }

        public void Quit() { Platform.Quit(); }

        // ------------------------------------------------------------------ 玩法事件

        private void HandleEnemyDestroyed(Enemy e, bool byBomb)
        {
            AddScore(e.Score);
            GameEvents.RaiseEnemyKilled(e.Kind, byBomb);
            if (e.Kind == EnemyKind.Large) Platform.Vibrate(VibrateStrength.Medium);
        }

        private void HandleSupplyCollected(Supply s)
        {
            if (s.Kind == SupplyKind.Bomb)
            {
                if (Bombs < config.maxBombs)
                {
                    Bombs++;
                    GameEvents.RaiseBombCountChanged(Bombs);
                }
            }
            else
            {
                World.Player.SetDoubleBullet(config.doubleBulletDuration);
            }
            GameEvents.RaiseSupplyCollected(s.Kind);
            Platform.Vibrate(VibrateStrength.Light);
        }

        private void HandlePlayerCollided(Enemy e)
        {
            if (State != GameState.Playing) return;
            World.Player.Kill();
            Lives--;
            GameEvents.RaiseLivesChanged(Lives);
            GameEvents.RaisePlayerHit();
            Platform.Vibrate(VibrateStrength.Heavy);
            _dyingTimer = PlayerDyingDuration;
            SetState(GameState.PlayerDying);
        }

        private void OnDyingFinished()
        {
            if (Lives > 0)
            {
                World.Player.Spawn(config.spawnInvincible * 2f, false);
                Input.Reset();
                SetState(GameState.Playing);
                return;
            }
            GameOver();
        }

        private void GameOver()
        {
            IsNewRecord = Score > BestScore;
            if (IsNewRecord)
            {
                BestScore = Score;
                Platform.SaveInt(BestScoreKey, BestScore);
            }
            Platform.ReportScore(Score);
            Platform.OnGameOver(Score);
            SetState(GameState.GameOver);
        }

        public void AddScore(int value)
        {
            Score += value;
            GameEvents.RaiseScoreChanged(Score);

            int lv = LevelIndex;
            while (lv + 1 < config.levels.Length && Score >= config.levels[lv + 1].scoreThreshold) lv++;
            if (lv != LevelIndex)
            {
                LevelIndex = lv;
                GameEvents.RaiseLevelChanged(LevelIndex);
            }
        }

        private void SetState(GameState next, bool force = false)
        {
            if (State == next && !force) return;
            var prev = State;
            State = next;
            if (next != GameState.GameOver) _waitingForAd = false;
            GameEvents.RaiseStateChanged(prev, next);

            // 在玩家看不到卡顿的时机（结算 / 主界面）主动 GC，降低局内触发 GC 的概率
            if (next == GameState.GameOver || next == GameState.Home) System.GC.Collect();
        }
    }
}
