using System;
using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 屏幕适配：逻辑宽度固定（默认 9 单位），高度随屏幕比例变化。
    /// 宽高比超过 maxAspect（PC、平板横屏、网页）时，把相机视口限制为竖屏并加左右黑边。
    /// </summary>
    public class WorldBounds : MonoBehaviour
    {
        public static float Left { get; private set; }
        public static float Right { get; private set; }
        public static float Top { get; private set; }
        public static float Bottom { get; private set; }
        public static float Width { get { return Right - Left; } }
        public static float Height { get { return Top - Bottom; } }

        /// <summary>视口发生变化（旋转屏幕、调整窗口大小）。</summary>
        public static event Action Changed;

        public Camera GameCamera { get; private set; }

        private GameConfig _cfg;
        private Camera _letterboxCamera;
        private int _lastW, _lastH;
        private Rect _lastSafeArea;

        public void Init(GameConfig cfg, Camera cam)
        {
            _cfg = cfg;
            GameCamera = cam;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.76f, 0.79f, 0.8f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.depth = 0;

            // 负责清黑边的底层相机
            var go = new GameObject("LetterboxCamera");
            go.transform.SetParent(transform, false);
            _letterboxCamera = go.AddComponent<Camera>();
            _letterboxCamera.depth = -100;
            _letterboxCamera.cullingMask = 0;
            _letterboxCamera.clearFlags = CameraClearFlags.SolidColor;
            _letterboxCamera.backgroundColor = cfg.letterboxColor;
            _letterboxCamera.orthographic = true;

            Refresh(true);
        }

        private void LateUpdate()
        {
            Refresh(false);
        }

        public void Refresh(bool force)
        {
            if (GameCamera == null) return;
            int w = Screen.width, h = Screen.height;
            if (!force && w == _lastW && h == _lastH && Screen.safeArea == _lastSafeArea) return;
            _lastW = w;
            _lastH = h;
            _lastSafeArea = Screen.safeArea;
            if (w <= 0 || h <= 0) return;

            float screenAspect = (float)w / h;
            float aspect = Mathf.Min(screenAspect, _cfg.maxAspect);
            float viewportWidth = aspect / screenAspect; // 0..1
            GameCamera.rect = new Rect((1f - viewportWidth) * 0.5f, 0f, viewportWidth, 1f);

            float halfW = _cfg.worldWidth * 0.5f;
            float halfH = halfW / aspect;
            GameCamera.orthographicSize = halfH;

            Left = -halfW;
            Right = halfW;
            Top = halfH;
            Bottom = -halfH;

            if (Changed != null) Changed();
        }

        public static Vector2 Clamp(Vector2 p, Vector2 halfExtents)
        {
            p.x = Mathf.Clamp(p.x, Left + halfExtents.x, Right - halfExtents.x);
            p.y = Mathf.Clamp(p.y, Bottom + halfExtents.y, Top - halfExtents.y);
            return p;
        }

        public static void ClearEvents() { Changed = null; }
    }
}
