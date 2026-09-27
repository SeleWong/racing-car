using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 键盘 / 手柄（PC、主机、电视盒子、Web）。
    /// 方向键 / WASD / 手柄左摇杆移动；空格 / B / 手柄 A 放炸弹；Esc / P / 手柄 Start 暂停。
    /// Android 返回键在 Unity 中映射为 Escape，因此返回键 = 暂停。
    /// </summary>
    public class KeyboardInput : IInputSource
    {
        public Vector2 MoveDelta { get; private set; }
        public bool BombPressed { get; private set; }
        public bool PausePressed { get; private set; }

        private bool _hasAxes = true;

        public void Reset()
        {
            MoveDelta = Vector2.zero;
            BombPressed = false;
            PausePressed = false;
        }

        public void Tick(Camera cam, GameConfig cfg)
        {
            Vector2 dir = Vector2.zero;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) dir.x -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) dir.x += 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) dir.y += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) dir.y -= 1f;

            // 手柄摇杆（默认 InputManager 的 Horizontal/Vertical 也包含 joystick 轴）
            if (dir == Vector2.zero && _hasAxes)
            {
                try
                {
                    dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                    if (dir.sqrMagnitude < 0.04f) dir = Vector2.zero;
                }
                catch (System.ArgumentException)
                {
                    _hasAxes = false; // 项目删除了默认轴
                }
            }

            if (dir.sqrMagnitude > 1f) dir.Normalize();
            MoveDelta = dir * cfg.keyboardSpeed * Time.unscaledDeltaTime;

            BombPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.B)
                          || Input.GetKeyDown(KeyCode.JoystickButton0);
            PausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)
                           || Input.GetKeyDown(KeyCode.JoystickButton7);
        }
    }
}
