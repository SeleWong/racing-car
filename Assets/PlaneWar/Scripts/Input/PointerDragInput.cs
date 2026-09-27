using UnityEngine;
using UnityEngine.EventSystems;

namespace PlaneWar
{
    /// <summary>
    /// 触屏 + 鼠标拖拽（原版操作）：按住屏幕任意位置拖动，飞机按手指的相对位移移动，
    /// 手指不会挡住飞机。多指时只跟踪第一根手指。双击屏幕释放炸弹。
    /// 适用于 iOS / Android / 微信小游戏 / WebGL / PC。
    /// </summary>
    public class PointerDragInput : IInputSource
    {
        public Vector2 MoveDelta { get; private set; }
        public bool BombPressed { get; private set; }
        public bool PausePressed { get { return false; } }

        private int _fingerId = -1;
        private bool _mouseDragging;
        private Vector3 _lastScreenPos;
        private float _lastTapTime = -10f;
        private Vector3 _lastTapPos;

        public void Reset()
        {
            _fingerId = -1;
            _mouseDragging = false;
            MoveDelta = Vector2.zero;
            BombPressed = false;
        }

        public void Tick(Camera cam, GameConfig cfg)
        {
            MoveDelta = Vector2.zero;
            BombPressed = false;
            if (cam == null) return;

            if (Input.touchSupported && Input.touchCount > 0)
            {
                TickTouch(cam, cfg);
                _mouseDragging = false;
            }
            else
            {
                _fingerId = -1;
                if (Input.mousePresent) TickMouse(cam, cfg);
            }
        }

        private void TickTouch(Camera cam, GameConfig cfg)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (_fingerId == -1 && t.phase == TouchPhase.Began)
                {
                    if (IsOverUI(t.fingerId)) continue;
                    _fingerId = t.fingerId;
                    _lastScreenPos = t.position;
                    CheckDoubleTap(t.position, cfg);
                    continue;
                }

                if (t.fingerId != _fingerId) continue;

                if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    Accumulate(cam, cfg, t.position);
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    Accumulate(cam, cfg, t.position);
                    _fingerId = -1;
                }
            }
        }

        private void TickMouse(Camera cam, GameConfig cfg)
        {
            Vector3 pos = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                if (IsOverUI(-1)) return;
                _mouseDragging = true;
                _lastScreenPos = pos;
                CheckDoubleTap(pos, cfg);
                return;
            }

            if (_mouseDragging && Input.GetMouseButton(0))
            {
                Accumulate(cam, cfg, pos);
            }

            if (Input.GetMouseButtonUp(0)) _mouseDragging = false;
        }

        private void Accumulate(Camera cam, GameConfig cfg, Vector3 screenPos)
        {
            Vector3 a = cam.ScreenToWorldPoint(new Vector3(_lastScreenPos.x, _lastScreenPos.y, 10f));
            Vector3 b = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
            MoveDelta += (Vector2)(b - a) * cfg.dragSensitivity;
            _lastScreenPos = screenPos;
        }

        private void CheckDoubleTap(Vector3 pos, GameConfig cfg)
        {
            if (!cfg.doubleTapBomb) return;
            float now = Time.unscaledTime;
            float maxDist = Mathf.Max(Screen.width, Screen.height) * 0.08f;
            if (now - _lastTapTime <= cfg.doubleTapTime && (pos - _lastTapPos).magnitude < maxDist)
            {
                BombPressed = true;
                _lastTapTime = -10f;
            }
            else
            {
                _lastTapTime = now;
                _lastTapPos = pos;
            }
        }

        private static bool IsOverUI(int pointerId)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return pointerId < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(pointerId);
        }
    }
}
