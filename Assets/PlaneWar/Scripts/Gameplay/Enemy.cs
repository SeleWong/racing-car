using UnityEngine;

namespace PlaneWar
{
    /// <summary>敌机：直线下落；中/大飞机受击闪白；被击毁后播放爆炸并淡出。</summary>
    public class Enemy : MonoBehaviour
    {
        public EnemyKind Kind { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public int Score { get { return _cfg.score; } }
        public bool IsDying { get; private set; }
        public EnemyTypeConfig Config { get { return _cfg; } }

        public Rect Hitbox
        {
            get { return HitBox.FromCenter(transform.position, Vector2.Scale(_cfg.size, _cfg.hitboxScale)); }
        }

        /// <summary>是否已进入屏幕（炸弹只清除屏幕内的敌机）。</summary>
        public bool IsOnScreen
        {
            get { return transform.position.y - _cfg.size.y * 0.5f < WorldBounds.Top; }
        }

        private const float HitFlashTime = 0.08f;
#if UNITY_EDITOR
        private static readonly string[] EditorNames = { "Enemy_Small", "Enemy_Medium", "Enemy_Large" };
#endif

        private SpriteRenderer _sr;
        private EnemyTypeConfig _cfg;
        private Sprite _normal, _hit;
        private float _speed;
        private float _hitTimer;
        private float _dieTimer;
        private Vector3 _deathPos;

        public static Enemy Create()
        {
            var go = new GameObject("Enemy");
            var e = go.AddComponent<Enemy>();
            e._sr = go.AddComponent<SpriteRenderer>();
            return e;
        }

        public void Setup(EnemyTypeConfig cfg, Sprite normal, Sprite hit, Vector2 pos, float speed)
        {
            _cfg = cfg;
            Kind = cfg.kind;
            MaxHp = Hp = Mathf.Max(1, cfg.hp);
            _normal = normal;
            _hit = hit;
            _speed = speed;
            _hitTimer = 0f;
            _dieTimer = 0f;
            IsDying = false;
            _sr.sprite = normal;
            _sr.color = Color.white;
            // 大飞机画在最底层，小飞机在上
            _sr.sortingOrder = 10 - (int)cfg.kind;
            transform.position = new Vector3(pos.x, pos.y, 0f);
            transform.localScale = Vector3.one;
#if UNITY_EDITOR
            gameObject.name = EditorNames[(int)cfg.kind]; // 仅编辑器显示，避免运行时字符串分配
#endif
        }

        /// <returns>false = 需要回收（飞出屏幕或爆炸动画结束）</returns>
        public bool Tick(float dt)
        {
            if (IsDying)
            {
                _dieTimer += dt;
                float t = Mathf.Clamp01(_dieTimer / Mathf.Max(0.01f, _cfg.dieDuration));
                _sr.color = new Color(1f, 1f, 1f, 1f - t);
                // 爆炸时略微抖动（原版的破碎帧效果）
                float shake = (1f - t) * 0.012f * _cfg.size.x;
                transform.localScale = Vector3.one * (1f + t * 0.1f);
                transform.position = _deathPos + new Vector3(Mathf.Sin(_dieTimer * 90f) * shake, Mathf.Cos(_dieTimer * 70f) * shake, 0f);
                return _dieTimer < _cfg.dieDuration;
            }

            Vector3 p = transform.position;
            p.y -= _speed * dt;
            transform.position = p;

            if (_hitTimer > 0f)
            {
                _hitTimer -= dt;
                if (_hitTimer <= 0f) _sr.sprite = _normal;
            }

            return p.y + _cfg.size.y * 0.5f > WorldBounds.Bottom;
        }

        /// <returns>true = 本次伤害击毁了敌机</returns>
        public bool TakeDamage(int damage)
        {
            if (IsDying) return false;
            Hp -= damage;
            if (Hp <= 0)
            {
                Hp = 0;
                Kill();
                return true;
            }
            if (_hit != null && _hit != _normal)
            {
                _sr.sprite = _hit;
                _hitTimer = HitFlashTime;
            }
            return false;
        }

        public void Kill()
        {
            if (IsDying) return;
            IsDying = true;
            _dieTimer = 0f;
            _deathPos = transform.position;
            _hitTimer = 0f;
            _sr.sprite = _normal;
        }
    }
}
