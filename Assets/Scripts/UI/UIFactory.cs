// ---------------------------------------------------------------------------
// UIFactory —— 运行时构建全部 UI(场景零 UI 节点)。
// 参考分辨率 900×1600;所有纯色块使用 SpriteFactory.White 底图。
// 必须在 SpriteFactory.Ready 之后调用。
// ---------------------------------------------------------------------------
using PlaneWar.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace PlaneWar.UI
{
    public static class UiColors
    {
        public static readonly Color Primary = new Color(0.18f, 0.49f, 0.96f);
        public static readonly Color PrimaryPressed = new Color(0.13f, 0.36f, 0.75f);
        public static readonly Color Secondary = new Color(0.12f, 0.23f, 0.38f);
        public static readonly Color Danger = new Color(0.55f, 0.18f, 0.2f);
        public static readonly Color Dim = new Color(0.02f, 0.03f, 0.09f, 0.8f);
        public static readonly Color PanelBg = new Color(0.07f, 0.10f, 0.19f, 0.97f);
        public static readonly Color Title = Color.white;
        public static readonly Color Accent = new Color(0.5f, 0.82f, 1f);
        public static readonly Color Gold = new Color(1f, 0.85f, 0.4f);
    }

    public static class UIFactory
    {
        private static Font _font;
        private static Font Arial
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        // ------------------------- 基础图元 -------------------------

        public static RectTransform FullPanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = SpriteFactory.White;
            img.color = color;
            img.raycastTarget = true; // 面板阻挡背后的游戏拖拽
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Solid(Transform parent, string name, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = SpriteFactory.White;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Label(Transform parent, string name, string content, int fontSize,
                                 Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Arial;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            return t;
        }

        /// <summary>居中定位快捷方式。</summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static Button Button(Transform parent, string name, string label,
                                    float x, float y, float w, float h,
                                    Color bg, int fontSize, out Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = SpriteFactory.White;
            img.color = bg;
            img.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = img;
            Place((RectTransform)go.transform, x, y, w, h);

            labelText = Label(go.transform, "Label", label, fontSize, Color.white);
            var lrt = (RectTransform)labelText.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            return btn;
        }

        // ------------------------- 主菜单 -------------------------

        public static MenuPanel BuildMenu(RectTransform canvas, UIManager ui)
        {
            var root = FullPanel(canvas, "MenuPanel", UiColors.Dim).gameObject;
            var panel = root.AddComponent<MenuPanel>();

            panel.Title = Label(root.transform, "Title", "飞 机 大 战", 108, UiColors.Title);
            Place(panel.Title.rectTransform, 0, 430, 820, 150);
            panel.Title.fontStyle = FontStyle.Bold;

            panel.Subtitle = Label(root.transform, "Subtitle", "微信飞机大战 · 完整逻辑复刻", 34, UiColors.Accent);
            Place(panel.Subtitle.rectTransform, 0, 320, 820, 60);

            Text startLabel, onlineLabel;
            panel.StartButton = Button(root.transform, "StartButton", "开 始 游 戏",
                                       0, 80, 480, 112, UiColors.Primary, 48, out startLabel);
            panel.StartLabel = startLabel;
            panel.StartButton.onClick.AddListener(ui.RaiseStartLocal);

            panel.OnlineButton = Button(root.transform, "OnlineButton", "联机模式 · 多端同局",
                                        0, -70, 480, 92, UiColors.Secondary, 34, out onlineLabel);
            panel.OnlineLabel = onlineLabel;
            panel.OnlineButton.onClick.AddListener(ui.RaiseStartOnline);

            panel.HighScore = Label(root.transform, "HighScore", "最高分 --", 30, UiColors.Gold);
            Place(panel.HighScore.rectTransform, 0, -170, 820, 50);

            panel.ErrorText = Label(root.transform, "Error", "", 26, new Color(1f, 0.45f, 0.4f));
            Place(panel.ErrorText.rectTransform, 0, -235, 860, 44);

            var version = Label(root.transform, "Version",
                                "Unity 2019+ · 确定性仿真内核 · 单机 / 联机多端接入",
                                22, new Color(1f, 1f, 1f, 0.35f));
            var vrt = version.rectTransform;
            vrt.anchorMin = new Vector2(0.5f, 0f);
            vrt.anchorMax = new Vector2(0.5f, 0f);
            vrt.pivot = new Vector2(0.5f, 0f);
            vrt.anchoredPosition = new Vector2(0, 34);
            vrt.sizeDelta = new Vector2(880, 40);
            return panel;
        }

        // ------------------------- HUD -------------------------

        public static HudPanel BuildHud(RectTransform canvas, UIManager ui)
        {
            var rootGo = new GameObject("HudPanel", typeof(RectTransform));
            rootGo.transform.SetParent(canvas, false);
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            var panel = rootGo.AddComponent<HudPanel>();

            // 顶栏
            var topbar = Solid(root, "TopBar", new Color(0f, 0f, 0f, 0.38f));
            var brt = topbar.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(0f, 118f);

            panel.Score = Label(root, "Score", "0", 58, Color.white, TextAnchor.MiddleLeft);
            panel.Score.fontStyle = FontStyle.Bold;
            var srt = panel.Score.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(34f, -24f);
            srt.sizeDelta = new Vector2(420f, 74f);

            // 生命(心形图标×5)
            var heartsRoot = new GameObject("Hearts", typeof(RectTransform));
            heartsRoot.transform.SetParent(root, false);
            var hrt = (RectTransform)heartsRoot.transform;
            hrt.anchorMin = new Vector2(1f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(1f, 1f);
            hrt.anchoredPosition = new Vector2(-150f, -36f);
            hrt.sizeDelta = new Vector2(280f, 48f);
            panel.Hearts = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var heart = Solid(heartsRoot.transform, "Heart" + i, new Color(1f, 0.55f, 0.6f));
                heart.sprite = SpriteFactory.PowerLife;
                var rt = heart.rectTransform;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(24f + i * 52f, 0f);
                rt.sizeDelta = new Vector2(46f, 46f);
                panel.Hearts[i] = heart;
            }

            // 暂停按钮
            Text pauseLabel;
            panel.PauseButton = Button(root, "PauseButton", "II", 0, 0, 84f, 84f,
                                       new Color(0f, 0f, 0f, 0.42f), 44, out pauseLabel);
            pauseLabel.fontStyle = FontStyle.Bold;
            var prt = (RectTransform)panel.PauseButton.transform;
            prt.anchorMin = new Vector2(1f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 1f);
            prt.anchoredPosition = new Vector2(-30f, -20f);
            prt.sizeDelta = new Vector2(84f, 84f);
            panel.PauseButton.onClick.AddListener(ui.RaisePauseToggle);

            // 双倍火力条
            panel.DoubleBarRoot = (RectTransform)new GameObject("DoubleBar", typeof(RectTransform)).transform;
            panel.DoubleBarRoot.SetParent(root, false);
            var dbrt = panel.DoubleBarRoot;
            dbrt.anchorMin = new Vector2(0.5f, 0f);
            dbrt.anchorMax = new Vector2(0.5f, 0f);
            dbrt.pivot = new Vector2(0.5f, 0f);
            dbrt.anchoredPosition = new Vector2(0f, 46f);
            dbrt.sizeDelta = new Vector2(460f, 26f);
            var barBg = Solid(dbrt, "Bg", new Color(0f, 0f, 0f, 0.5f));
            Stretch(barBg.rectTransform);
            panel.DoubleFill = Solid(dbrt, "Fill", new Color(0.35f, 0.8f, 1f));
            var frt = panel.DoubleFill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            var barLabel = Label(dbrt, "Label", "双倍火力", 22, UiColors.Accent);
            Place(barLabel.rectTransform, 0f, 30f, 300f, 34f);
            dbrt.gameObject.SetActive(false);
            return panel;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ------------------------- 结算面板 -------------------------

        public static GameOverPanel BuildGameOver(RectTransform canvas, UIManager ui)
        {
            var root = FullPanel(canvas, "GameOverPanel", new Color(0.02f, 0.02f, 0.06f, 0.82f)).gameObject;
            var panel = root.AddComponent<GameOverPanel>();

            var box = Solid(root.transform, "Box", UiColors.PanelBg);
            Place(box.rectTransform, 0, 40, 640, 820);

            panel.Title = Label(box.transform, "Title", "游戏结束", 64, UiColors.Title);
            panel.Title.fontStyle = FontStyle.Bold;
            Place(panel.Title.rectTransform, 0, 310, 560, 90);

            panel.Score = Label(box.transform, "Score", "0", 104, UiColors.Gold);
            panel.Score.fontStyle = FontStyle.Bold;
            Place(panel.Score.rectTransform, 0, 180, 560, 130);

            panel.NewRecord = Label(box.transform, "NewRecord", "★ 新纪录 ★", 36, new Color(1f, 0.45f, 0.5f));
            Place(panel.NewRecord.rectTransform, 0, 92, 560, 50);

            panel.HighScore = Label(box.transform, "HighScore", "最高分 0", 30, Color.white);
            Place(panel.HighScore.rectTransform, 0, 34, 560, 44);

            panel.Kills = Label(box.transform, "Kills", "击坠:0", 30, new Color(0.8f, 0.9f, 1f));
            Place(panel.Kills.rectTransform, 0, -22, 560, 44);

            panel.TimeSurvived = Label(box.transform, "Time", "生存时间 00:00", 30, new Color(0.8f, 0.9f, 1f));
            Place(panel.TimeSurvived.rectTransform, 0, -78, 560, 44);

            Text restartLabel, menuLabel;
            panel.RestartButton = Button(box.transform, "RestartButton", "再 来 一 局",
                                         0, -200, 440, 104, UiColors.Primary, 44, out restartLabel);
            panel.RestartButton.onClick.AddListener(ui.RaiseRestart);

            panel.MenuButton = Button(box.transform, "MenuButton", "返回主菜单",
                                      0, -330, 440, 86, UiColors.Secondary, 34, out menuLabel);
            panel.MenuButton.onClick.AddListener(ui.RaiseBackToMenu);
            return panel;
        }

        // ------------------------- 连接中 -------------------------

        public static ConnectingPanel BuildConnecting(RectTransform canvas, UIManager ui)
        {
            var root = FullPanel(canvas, "ConnectingPanel", UiColors.Dim).gameObject;
            var panel = root.AddComponent<ConnectingPanel>();

            panel.Status = Label(root.transform, "Status", "正在连接服务器…", 42, Color.white);
            Place(panel.Status.rectTransform, 0, 60, 820, 70);

            Text cancelLabel;
            panel.CancelButton = Button(root.transform, "CancelButton", "取 消",
                                        0, -90, 320, 86, UiColors.Danger, 36, out cancelLabel);
            panel.CancelButton.onClick.AddListener(ui.RaiseCancelOnline);
            return panel;
        }

        // ------------------------- 暂停覆盖层 -------------------------

        public static PauseOverlay BuildPauseOverlay(RectTransform canvas, UIManager ui)
        {
            var root = FullPanel(canvas, "PauseOverlay", new Color(0f, 0f, 0f, 0.62f)).gameObject;
            var panel = root.AddComponent<PauseOverlay>();

            var title = Label(root.transform, "Title", "已 暂 停", 72, Color.white);
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, 0, 260, 600, 100);

            Text l1, l2, l3;
            panel.ResumeButton = Button(root.transform, "ResumeButton", "继 续 游 戏",
                                        0, 80, 440, 104, UiColors.Primary, 44, out l1);
            panel.ResumeButton.onClick.AddListener(ui.RaiseResume);

            panel.RestartButton = Button(root.transform, "RestartButton", "重 新 开 始",
                                         0, -60, 440, 92, UiColors.Secondary, 38, out l2);
            panel.RestartButton.onClick.AddListener(ui.RaiseRestart);

            panel.MenuButton = Button(root.transform, "MenuButton", "返回主菜单",
                                      0, -190, 440, 92, UiColors.Secondary, 38, out l3);
            panel.MenuButton.onClick.AddListener(ui.RaiseBackToMenu);
            return panel;
        }
    }
}
