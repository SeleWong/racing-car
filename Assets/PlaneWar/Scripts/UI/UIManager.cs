using UnityEngine;
using UnityEngine.UI;

namespace PlaneWar
{
    /// <summary>
    /// 全部界面：加载、主界面、游戏 HUD、暂停、结算。纯代码构建，只通过 GameEvents 和
    /// GameManager 的公开方法与逻辑层交互。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private GameManager _gm;
        private UIFactory _f;
        private Canvas _canvas;

        public UIFactory Factory { get { return _f; } }
        public RectTransform SafeRoot { get; private set; }

        // 预生成的常用字符串，避免运行时拼接产生 GC
        private static readonly string[] BombCountStrings = BuildStrings("×", 0, 9, "");
        private static readonly string[] LivesStrings = BuildStrings("生命 ×", 0, 9, "");
        private string[] _doubleStrings;
        private int _pendingScore = -1;
        private int _shownScore = -1;
        private int _shownDoubleSeconds = -1;

        private GameObject _loading, _home, _hud, _pause, _gameOver;

        // 主界面
        private Text _homeBest;
        private Button _homeMute;

        // HUD
        private Text _score;
        private Button _bombButton;
        private Text _bombCount;
        private Text _doubleHint;
        private Text _lives;
        private Text _levelToast;
        private float _levelToastTimer;
        private float _scorePunch;

        // 暂停
        private Button _pauseMute;

        // 结算
        private Text _goScore, _goBest;
        private GameObject _goNewRecord;
        private Button _goRevive, _goShare;

        public void Init(GameManager gm, Camera cam)
        {
            _gm = gm;
            _f = new UIFactory(UIFactory.LoadDefaultFont(gm.Config), gm.Sprites);
            _f.OnAnyButton = () => gm.Audio.PlayButton();

            // Canvas：Screen Space - Camera，自动跟随游戏相机视口（PC 宽屏时 UI 也在竖屏区域内）
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = cam;
            _canvas.planeDistance = 1f;
            _canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // 逻辑宽度固定，与世界坐标一致
            gameObject.AddComponent<GraphicRaycaster>();
            gameObject.layer = 5;

            var safe = UIFactory.Stretch(UIFactory.Rect("SafeArea", transform));
            SafeRoot = safe;
            var fitter = safe.gameObject.AddComponent<SafeAreaFitter>();
            fitter.TargetCamera = cam;
            fitter.Apply(true);

            BuildHud(safe);
            BuildHome(safe);
            BuildPause(safe);
            BuildGameOver(safe);
            BuildLoading(transform);

            GameEvents.StateChanged += OnStateChanged;
            GameEvents.ScoreChanged += OnScoreChanged;
            GameEvents.BombCountChanged += OnBombChanged;
            GameEvents.DoubleBulletChanged += OnDoubleChanged;
            GameEvents.LivesChanged += OnLivesChanged;
            GameEvents.LevelChanged += OnLevelChanged;

            ShowOnly(null);
            PrewarmFont();
        }

        /// <summary>
        /// 预热动态字体：局内首次出现新字符（分数数字、“新纪录”等）会触发字体纹理重建，
        /// 导致所有文本重新生成网格而掉帧。启动时把会用到的字符按实际字号 / 字形一次性请求进纹理。
        /// </summary>
        private void PrewarmFont()
        {
            if (_f.Font == null || !_f.Font.dynamic) return;
            const string dynamicChars = "0123456789×sLV.：:！!双倍火力难度提升生命最高分新纪录加载中… ";
            // UGUI 实际按 fontSize × Canvas.scaleFactor 生成字形（随分辨率变化）
            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                int size = Mathf.Max(1, Mathf.RoundToInt(t.fontSize * scale));
                _f.Font.RequestCharactersInTexture(t.text + dynamicChars, size, t.fontStyle);
            }
        }

        private void OnDestroy()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.ScoreChanged -= OnScoreChanged;
            GameEvents.BombCountChanged -= OnBombChanged;
            GameEvents.DoubleBulletChanged -= OnDoubleChanged;
            GameEvents.LivesChanged -= OnLivesChanged;
            GameEvents.LevelChanged -= OnLevelChanged;
        }

        public void ShowLoading(bool show) { if (_loading != null) _loading.SetActive(show); }

        private static string[] BuildStrings(string prefix, int from, int to, string suffix)
        {
            var arr = new string[to - from + 1];
            for (int i = from; i <= to; i++) arr[i - from] = prefix + i + suffix;
            return arr;
        }

        private void LateUpdate()
        {
            // 一帧内多次加分（炸弹清屏）只刷新一次文本 → 只生成一次字符串、只重建一次网格
            if (_pendingScore >= 0 && _pendingScore != _shownScore)
            {
                _shownScore = _pendingScore;
                _score.text = _shownScore.ToString();
            }
            _pendingScore = -1;
        }

        private void Update()
        {
            if (_scorePunch > 0f)
            {
                _scorePunch = Mathf.Max(0f, _scorePunch - Time.unscaledDeltaTime * 4f);
                _score.transform.localScale = Vector3.one * (1f + _scorePunch * 0.15f);
            }
            if (_levelToastTimer > 0f)
            {
                _levelToastTimer -= Time.unscaledDeltaTime;
                // 用 CanvasRenderer.SetAlpha 淡出：不会标脏顶点，不触发文本网格重建
                _levelToast.canvasRenderer.SetAlpha(Mathf.Clamp01(_levelToastTimer / 0.5f));
                if (_levelToastTimer <= 0f) _levelToast.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ 构建

        private void BuildLoading(Transform parent)
        {
            var bg = _f.Image("Loading", parent, _f.Sprites.White, new Color(0.76f, 0.79f, 0.8f));
            bg.raycastTarget = true;
            UIFactory.Stretch(bg.rectTransform);
            var t = _f.Text("Text", bg.transform, "加载中…", 36, UIFactory.TextDark);
            UIFactory.Stretch(t.rectTransform);
            _loading = bg.gameObject;
        }

        private void BuildHome(RectTransform parent)
        {
            var root = UIFactory.Stretch(UIFactory.Rect("Home", parent));
            _home = root.gameObject;

            var title = _f.Text("Title", root, "飞机大战", 96, UIFactory.TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(700, 140), new Vector2(0.5f, 0.5f));
            var sub = _f.Text("Sub", root, "PLANE  WAR", 30, new Color(0.35f, 0.38f, 0.42f));
            UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(0, -90), new Vector2(600, 50), new Vector2(0.5f, 0.5f));

            var hero = _f.Image("Hero", root, _f.Sprites.Player1, Color.white);
            hero.preserveAspect = true;
            UIFactory.Place(hero.rectTransform, new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(200, 250), new Vector2(0.5f, 0.5f));

            _homeBest = _f.Text("Best", root, "", 32, UIFactory.TextDark);
            UIFactory.Place(_homeBest.rectTransform, new Vector2(0.5f, 0.36f), Vector2.zero, new Vector2(600, 50), new Vector2(0.5f, 0.5f));

            var start = _f.Button("Start", root, "开始游戏", new Vector2(360, 96), () => _gm.StartGame(), 40);
            UIFactory.Place((RectTransform)start.transform, new Vector2(0.5f, 0.26f), Vector2.zero, new Vector2(360, 96), new Vector2(0.5f, 0.5f));

            float y = -120f;
            if (_gm.Platform.SupportsQuit)
            {
                var quit = _f.Button("Quit", root, "退出游戏", new Vector2(360, 80), () => _gm.Quit(), 32);
                UIFactory.Place((RectTransform)quit.transform, new Vector2(0.5f, 0.26f), new Vector2(0, y), new Vector2(360, 80), new Vector2(0.5f, 0.5f));
            }

            _homeMute = _f.Button("Mute", root, "", new Vector2(150, 64), () => { _gm.ToggleMute(); RefreshMuteLabels(); }, 26);
            UIFactory.Place((RectTransform)_homeMute.transform, new Vector2(1f, 1f), new Vector2(-24, -24), new Vector2(150, 64));

            var hint = _f.Text("Hint", root, GetControlsHint(), 24, new Color(0.35f, 0.38f, 0.42f));
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(660, 90), new Vector2(0.5f, 0f));
        }

        private void BuildHud(RectTransform parent)
        {
            var root = UIFactory.Stretch(UIFactory.Rect("HUD", parent));
            _hud = root.gameObject;

            var pause = _f.IconButton("Pause", root, _f.Sprites.PauseIcon, new Vector2(76, 76), () => _gm.TogglePause(),
                new Color(0.26f, 0.29f, 0.34f, 0.85f));
            UIFactory.Place((RectTransform)pause.transform, new Vector2(0f, 1f), new Vector2(20, -20), new Vector2(76, 76));

            // 频繁变化的元素放进独立子 Canvas：它们变化时只重建这个小 Canvas，
            // 不会让暂停按钮、炸弹按钮等静态元素一起重新合批
            var dyn = UIFactory.Stretch(UIFactory.Rect("HUDDynamic", root));
            dyn.gameObject.AddComponent<Canvas>();

            _score = _f.Text("Score", dyn, "0", 44, UIFactory.TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(_score.rectTransform, new Vector2(0f, 1f), new Vector2(112, -20), new Vector2(460, 76));

            _lives = _f.Text("Lives", dyn, "", 30, UIFactory.TextDark, TextAnchor.MiddleRight);
            UIFactory.Place(_lives.rectTransform, new Vector2(1f, 1f), new Vector2(-24, -20), new Vector2(240, 76));

            // 炸弹按钮（左下角，数量为 0 时隐藏，与原版一致）
            _bombButton = _f.IconButton("Bomb", root, _f.Sprites.BombIcon, new Vector2(104, 104), () => _gm.UseBomb(),
                new Color(1f, 1f, 1f, 0.55f));
            UIFactory.Place((RectTransform)_bombButton.transform, new Vector2(0f, 0f), new Vector2(24, 24), new Vector2(104, 104));
            _bombCount = _f.Text("Count", dyn, "×0", 40, UIFactory.TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(_bombCount.rectTransform, new Vector2(0f, 0f), new Vector2(136, 24), new Vector2(160, 104));

            _doubleHint = _f.Text("Double", dyn, "", 26, new Color(0.2f, 0.45f, 0.85f), TextAnchor.MiddleRight);
            UIFactory.Place(_doubleHint.rectTransform, new Vector2(1f, 0f), new Vector2(-24, 40), new Vector2(300, 60));

            _levelToast = _f.Text("LevelToast", dyn, "", 48, new Color(0.85f, 0.3f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(_levelToast.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(600, 80), new Vector2(0.5f, 0.5f));
            _levelToast.gameObject.SetActive(false);

            OnBombChanged(0);
        }

        private RectTransform BuildDialog(RectTransform parent, string name, Vector2 size, out GameObject rootGo)
        {
            var dim = _f.Image(name, parent, _f.Sprites.White, new Color(0f, 0f, 0f, 0.35f));
            dim.raycastTarget = true; // 挡住下层点击
            UIFactory.Stretch(dim.rectTransform, -2000f); // 覆盖安全区外
            rootGo = dim.gameObject;

            var border = _f.Image("Card", dim.transform, _f.Sprites.RoundRect, UIFactory.BorderColor, true);
            UIFactory.Place(border.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size, new Vector2(0.5f, 0.5f));
            border.gameObject.AddComponent<PopIn>();
            var card = _f.Image("Fill", border.transform, _f.Sprites.RoundRect, UIFactory.PanelFill, true);
            UIFactory.Stretch(card.rectTransform, 5f);
            return card.rectTransform;
        }

        private void BuildPause(RectTransform parent)
        {
            var card = BuildDialog(parent, "Pause", new Vector2(520, 640), out _pause);
            var title = _f.Text("Title", card, "游戏暂停", 52, UIFactory.TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(460, 80), new Vector2(0.5f, 1f));

            var size = new Vector2(360, 88);
            AddCardButton(card, "Resume", "继续游戏", -170, size, () => _gm.Resume());
            AddCardButton(card, "Restart", "重新开始", -280, size, () => _gm.Restart());
            AddCardButton(card, "Home", "回到主页", -390, size, () => _gm.GoHome());
            _pauseMute = AddCardButton(card, "Mute", "", -500, size, () => { _gm.ToggleMute(); RefreshMuteLabels(); });
        }

        private void BuildGameOver(RectTransform parent)
        {
            var card = BuildDialog(parent, "GameOver", new Vector2(560, 860), out _gameOver);
            _gameOver.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var title = _f.Text("Title", card, "飞机大战分数", 44, UIFactory.TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(500, 70), new Vector2(0.5f, 1f));

            _goScore = _f.Text("Score", card, "0", 88, UIFactory.TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(_goScore.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(520, 120), new Vector2(0.5f, 1f));

            var rec = _f.Text("NewRecord", card, "新纪录！", 34, new Color(0.9f, 0.3f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(rec.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -262), new Vector2(400, 50), new Vector2(0.5f, 1f));
            _goNewRecord = rec.gameObject;

            _goBest = _f.Text("Best", card, "", 30, new Color(0.35f, 0.38f, 0.42f));
            UIFactory.Place(_goBest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -318), new Vector2(500, 50), new Vector2(0.5f, 1f));

            var size = new Vector2(380, 88);
            _goRevive = AddCardButton(card, "Revive", "看视频复活", -400, size, OnReviveClicked);
            AddCardButton(card, "Restart", "重新开始", -505, size, () => _gm.Restart());
            _goShare = AddCardButton(card, "Share", "分享给好友", -610, size, () => _gm.Share());
            AddCardButton(card, "Home", "回到主页", -715, size, () => _gm.GoHome());
        }

        private Button AddCardButton(RectTransform card, string name, string label, float y, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var b = _f.Button(name, card, label, size, onClick);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0, y), size, new Vector2(0.5f, 1f));
            return b;
        }

        // ------------------------------------------------------------------ 事件

        private void OnStateChanged(GameState from, GameState to)
        {
            switch (to)
            {
                case GameState.Home:
                    _homeBest.text = "最高分：" + _gm.BestScore;
                    RefreshMuteLabels();
                    ShowOnly(_home);
                    break;
                case GameState.Playing:
                case GameState.PlayerDying:
                    ShowOnly(_hud);
                    break;
                case GameState.Paused:
                    RefreshMuteLabels();
                    ShowOnly(_hud);
                    _pause.SetActive(true);
                    break;
                case GameState.GameOver:
                    _goScore.text = _gm.Score.ToString();
                    _goBest.text = "最高分：" + _gm.BestScore;
                    _goNewRecord.SetActive(_gm.IsNewRecord && _gm.Score > 0);
                    _goRevive.gameObject.SetActive(_gm.CanRevive);
                    _goRevive.interactable = true;
                    _goShare.gameObject.SetActive(_gm.Platform.SupportsShare);
                    ShowOnly(_gameOver);
                    break;
            }
        }

        private void ShowOnly(GameObject panel)
        {
            _home.SetActive(panel == _home);
            _hud.SetActive(panel == _hud);
            _pause.SetActive(panel == _pause);
            _gameOver.SetActive(panel == _gameOver);
        }

        private void OnReviveClicked()
        {
            _goRevive.interactable = false; // 防止重复点击，广告回调后状态会切换
            _gm.RequestRevive();
        }

        private void OnScoreChanged(int score)
        {
            _pendingScore = score;
            if (score > 0) _scorePunch = 1f;
            else if (_shownScore != 0) { _shownScore = 0; _score.text = "0"; }
        }

        private void OnBombChanged(int count)
        {
            bool show = count > 0;
            _bombButton.gameObject.SetActive(show);
            _bombCount.gameObject.SetActive(show);
            _bombCount.text = count < BombCountStrings.Length ? BombCountStrings[count] : "×" + count;
        }

        private void OnDoubleChanged(float remaining)
        {
            int sec = remaining > 0f ? Mathf.CeilToInt(remaining) : 0;
            if (sec == _shownDoubleSeconds) return;
            _shownDoubleSeconds = sec;
            if (_doubleStrings == null)
                _doubleStrings = BuildStrings("双倍火力 ", 0, Mathf.CeilToInt(_gm.Config.doubleBulletDuration) + 1, "s");
            _doubleHint.text = sec <= 0 ? string.Empty
                : sec < _doubleStrings.Length ? _doubleStrings[sec] : "双倍火力 " + sec + "s";
        }

        private void OnLivesChanged(int lives)
        {
            int v = Mathf.Max(0, lives);
            _lives.text = _gm.Config.playerLives <= 1 ? string.Empty
                : v < LivesStrings.Length ? LivesStrings[v] : "生命 ×" + v;
        }

        private void OnLevelChanged(int level)
        {
            if (level <= 0) return;
            _levelToast.text = "难度提升  LV." + (level + 1);
            _levelToast.gameObject.SetActive(true);
            _levelToast.canvasRenderer.SetAlpha(1f);
            _levelToastTimer = 1.8f;
        }

        private void RefreshMuteLabels()
        {
            string label = _gm.Audio.Muted ? "音效：关" : "音效：开";
            UIFactory.SetLabel(_homeMute, label);
            UIFactory.SetLabel(_pauseMute, label);
        }

        private static string GetControlsHint()
        {
            if (Application.isMobilePlatform)
                return "按住屏幕拖动飞机躲避敌机\n双击屏幕或点击左下角炸弹清屏";
#if UNITY_STANDALONE || UNITY_EDITOR
            return "鼠标拖动 / 方向键 / WASD / 手柄 移动飞机\n空格键或双击释放炸弹，Esc 暂停";
#else
            return "按住屏幕拖动飞机躲避敌机\n双击屏幕或点击炸弹清屏，Esc 暂停";
#endif
        }
    }
}
