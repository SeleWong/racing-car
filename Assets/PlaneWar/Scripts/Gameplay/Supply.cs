using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 补给包（双排子弹 / 炸弹）。原版运动轨迹：从顶部缓慢降下 → 向上回弹一小段 → 快速坠落出屏幕。
    /// </summary>
    public class Supply : MonoBehaviour
    {
        public SupplyKind Kind { get; private set; }
        public Rect Hitbox { get { return HitBox.FromCenter(transform.position, _size * 0.85f); } }

        private enum Phase { Enter, Bounce, Fall }

        private SpriteRenderer _sr;
        private Vector2 _size;
        private Phase _phase;
        private float _phaseTimer;
        private float _enterSpeed, _fallSpeed;
        private float _stopY;
        private float _swayTime;
        private float _baseX;

        public static Supply Create()
        {
            var go = new GameObject("Supply");
            var s = go.AddComponent<Supply>();
            s._sr = go.AddComponent<SpriteRenderer>();
            s._sr.sortingOrder = 15;
            return s;
        }

        public void Setup(SupplyKind kind, Sprite sprite, float x, Vector2 size, float enterSpeed, float fallSpeed)
        {
            Kind = kind;
            _sr.sprite = sprite;
            _size = size;
            _enterSpeed = enterSpeed;
            _fallSpeed = fallSpeed;
            _phase = Phase.Enter;
            _phaseTimer = 0f;
            _swayTime = 0f;
            _baseX = x;
            _stopY = WorldBounds.Top - WorldBounds.Height * 0.3f;
            transform.position = new Vector3(x, WorldBounds.Top + size.y * 0.5f, 0f);
            gameObject.name = "Supply_" + kind;
        }

        public bool Tick(float dt)
        {
            Vector3 p = transform.position;
            _phaseTimer += dt;
            _swayTime += dt;
            switch (_phase)
            {
                case Phase.Enter:
                    p.y -= _enterSpeed * dt;
                    if (p.y <= _stopY) { _phase = Phase.Bounce; _phaseTimer = 0f; }
                    break;
                case Phase.Bounce:
                    p.y += _enterSpeed * 0.5f * dt;
                    if (_phaseTimer > 0.35f) { _phase = Phase.Fall; _phaseTimer = 0f; }
                    break;
                default:
                    p.y -= _fallSpeed * dt;
                    break;
            }
            // 降落伞轻微摆动
            p.x = _baseX + Mathf.Sin(_swayTime * 3f) * 0.08f;
            p.x = Mathf.Clamp(p.x, WorldBounds.Left + _size.x * 0.5f, WorldBounds.Right - _size.x * 0.5f);
            transform.position = p;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_swayTime * 3f) * 6f);
            return p.y + _size.y * 0.5f > WorldBounds.Bottom;
        }
    }
}
