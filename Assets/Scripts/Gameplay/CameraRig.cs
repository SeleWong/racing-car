// ---------------------------------------------------------------------------
// CameraRig —— 正交相机:把 9×16 的世界恰好拍满屏幕。
// 宽高比大于 9:16(平板/桌面)时两侧自然留黑;小于时(极少见)自动缩小。
// ---------------------------------------------------------------------------
using PlaneWar.Core;
using UnityEngine;

namespace PlaneWar.Gameplay
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        private Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.backgroundColor = new Color(0.012f, 0.02f, 0.05f);
            Apply();
        }

        private void Update()
        {
            Apply();
        }

        private void Apply()
        {
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float size = GameConfig.WorldHeight * 0.5f;
            float need = GameConfig.WorldWidth * 0.5f / Mathf.Max(0.1f, aspect);
            if (need > size) size = need; // 屏幕过窄时保证世界宽度可见
            _cam.orthographicSize = size;
            transform.position = new Vector3(GameConfig.WorldWidth * 0.5f,
                                             GameConfig.WorldHeight * 0.5f, -10f);
        }
    }
}
