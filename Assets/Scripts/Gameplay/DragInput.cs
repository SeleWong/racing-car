// ---------------------------------------------------------------------------
// DragInput —— 复刻原版"拖拽移动"手感(触摸/鼠标通用):
//   按下时记录:按下点(屏幕) + 玩家当前位置(世界);
//   拖动中:目标 = 按下时玩家位置 + (当前点-按下点) × 灵敏度;
//   抬起后玩家停在原地。
// 挂在铺满全屏的透明 Image 上,经 EventSystem 接收 IPointerDown/Drag/Up。
// 仅处理第一个触点(原版行为),其余触点忽略。
// ---------------------------------------------------------------------------
using System;
using PlaneWar.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlaneWar.Gameplay
{
    public sealed class DragInput : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
    {
        /// <summary>取玩家当前世界锚点(由 GameView 注入)。</summary>
        public Func<Vector2> GetAnchorWorld;

        /// <summary>拖拽灵敏度(见 GAME_SPEC §3)。</summary>
        public float Sensitivity = GameConfig.DragSensitivity;

        public Camera WorldCamera;

        private int _activePointer = -1;
        private Vector2 _downScreen;
        private Vector2 _downAnchor;

        public bool IsDragging { get; private set; }
        public Vector2 TargetWorld { get; private set; }

        public void OnPointerDown(PointerEventData e)
        {
            if (IsDragging) return;                 // 只处理第一个触点
            IsDragging = true;
            _activePointer = e.pointerId;
            _downScreen = e.position;
            _downAnchor = GetAnchorWorld != null ? GetAnchorWorld() : Vector2.zero;
            TargetWorld = _downAnchor;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!IsDragging || e.pointerId != _activePointer) return;
            Vector2 deltaWorld = ScreenDeltaToWorld(_downScreen, e.position);
            TargetWorld = _downAnchor + deltaWorld * Sensitivity;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!IsDragging || e.pointerId != _activePointer) return;
            IsDragging = false;
            _activePointer = -1;
        }

        public void OnCancel(BaseEventData e)
        {
            IsDragging = false;
            _activePointer = -1;
        }

        private Vector2 ScreenDeltaToWorld(Vector2 from, Vector2 to)
        {
            if (WorldCamera == null) return Vector2.zero;
            Vector3 a = WorldCamera.ScreenToWorldPoint(new Vector3(from.x, from.y, 10f));
            Vector3 b = WorldCamera.ScreenToWorldPoint(new Vector3(to.x, to.y, 10f));
            return new Vector2(b.x - a.x, b.y - a.y);
        }
    }
}
