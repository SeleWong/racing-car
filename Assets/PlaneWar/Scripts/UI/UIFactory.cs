using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlaneWar
{
    /// <summary>代码构建 UGUI 的辅助方法（参考分辨率 720×1280）。</summary>
    public class UIFactory
    {
        public static readonly Color TextDark = new Color(0.2f, 0.22f, 0.26f);
        public static readonly Color BorderColor = new Color(0.26f, 0.29f, 0.34f);
        public static readonly Color ButtonFill = new Color(0.93f, 0.94f, 0.95f);
        public static readonly Color PanelFill = new Color(0.86f, 0.88f, 0.9f, 0.97f);

        public readonly Font Font;
        public readonly SpriteLibrary Sprites;
        public Action OnAnyButton;

        public UIFactory(Font font, SpriteLibrary sprites)
        {
            Font = font;
            Sprites = sprites;
        }

        public static Font LoadDefaultFont(GameConfig cfg)
        {
            if (cfg.uiFont != null) return cfg.uiFont;
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>按锚点放置：anchor 为 (0..1, 0..1)，pos 为相对锚点的像素偏移。</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        public Image Image(string name, Transform parent, Sprite sprite, Color color, bool sliced = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        public Text Text(string name, Transform parent, string content, int size, Color color,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = content;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>原版风格按钮：深色描边的浅灰圆角矩形 + 深色文字。</summary>
        public Button Button(string name, Transform parent, string label, Vector2 size, UnityAction onClick, int fontSize = 34)
        {
            var border = Image(name, parent, Sprites.RoundRect, BorderColor, true);
            border.raycastTarget = true;
            border.rectTransform.sizeDelta = size;

            var fill = Image("Fill", border.transform, Sprites.RoundRect, ButtonFill, true);
            Stretch(fill.rectTransform, 4f);

            var text = Text("Label", border.transform, label, fontSize, TextDark);
            Stretch(text.rectTransform);

            var btn = border.gameObject.AddComponent<Button>();
            btn.targetGraphic = fill;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.75f, 0.77f, 0.8f);
            colors.highlightedColor = new Color(0.97f, 0.97f, 0.97f);
            btn.colors = colors;
            btn.onClick.AddListener(() =>
            {
                ClearSelection();
                if (OnAnyButton != null) OnAnyButton();
                if (onClick != null) onClick();
            });
            return btn;
        }

        /// <summary>纯图标圆形按钮（暂停、炸弹）。</summary>
        public Button IconButton(string name, Transform parent, Sprite icon, Vector2 size, UnityAction onClick, Color bg)
        {
            var root = Image(name, parent, Sprites.Circle, bg);
            root.raycastTarget = true;
            root.rectTransform.sizeDelta = size;
            var ic = Image("Icon", root.transform, icon, Color.white);
            ic.preserveAspect = true;
            Stretch(ic.rectTransform, size.x * 0.18f);
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = root;
            btn.onClick.AddListener(() =>
            {
                ClearSelection();
                if (OnAnyButton != null) OnAnyButton();
                if (onClick != null) onClick();
            });
            return btn;
        }

        /// <summary>点击后取消选中，避免空格键（Submit）再次触发按钮。</summary>
        private static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public static void SetLabel(Button b, string label)
        {
            var t = b.GetComponentInChildren<Text>(true);
            if (t != null) t.text = label;
        }
    }

    /// <summary>面板弹出动画（不受 timeScale 影响）。</summary>
    public class PopIn : MonoBehaviour
    {
        private float _t;

        private void OnEnable()
        {
            _t = 0f;
            transform.localScale = Vector3.one * 0.85f;
        }

        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / 0.18f);
            float s = 1f - Mathf.Pow(1f - _t, 3f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, s);
        }
    }

    /// <summary>把子节点限制在设备安全区域内（刘海屏、圆角、Home 指示条）。</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        public Camera TargetCamera;
        private Rect _lastSafe;
        private Rect _lastCam;

        private void Update() { Apply(false); }

        public void Apply(bool force)
        {
            if (TargetCamera == null) return;
            Rect safe = Screen.safeArea;
            Rect cam = TargetCamera.pixelRect;
            if (!force && safe == _lastSafe && cam == _lastCam) return;
            _lastSafe = safe;
            _lastCam = cam;
            if (cam.width <= 0 || cam.height <= 0) return;

            float xMin = Mathf.Max(safe.xMin, cam.xMin), xMax = Mathf.Min(safe.xMax, cam.xMax);
            float yMin = Mathf.Max(safe.yMin, cam.yMin), yMax = Mathf.Min(safe.yMax, cam.yMax);
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2((xMin - cam.xMin) / cam.width, (yMin - cam.yMin) / cam.height);
            rt.anchorMax = new Vector2((xMax - cam.xMin) / cam.width, (yMax - cam.yMin) / cam.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
