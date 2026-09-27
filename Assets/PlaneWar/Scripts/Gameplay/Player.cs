using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 玩家飞机：拖拽移动（限制在屏幕内）、自动射击、单/双排子弹、尾焰动画、无敌闪烁、爆炸。
    /// </summary>
    public class Player : MonoBehaviour
    {
        public bool IsAlive { get; private set; }
        public bool IsInvincible { get { return _invincibleTimer > 0f; } }
        public bool HasDoubleBullet { get { return _doubleTimer > 0f; } }
        public float DoubleBulletRemaining { get { return _doubleTimer; } }

        public Rect Hitbox { get { return HitBox.FromCenter(transform.position, _cfg.playerHitbox); } }

        private GameConfig _cfg;
        private SpriteLibrary _sprites;
        private GameWorld _world;
        private SpriteRenderer _sr;

        private float _fireTimer;
        private float _doubleTimer;
        private float _invincibleTimer;
        private float _animTimer;
        private bool _flameFrame;
        private float _lastDoubleReport;

        public void Init(GameConfig cfg, SpriteLibrary sprites, GameWorld world)
        {
            _cfg = cfg;
            _sprites = sprites;
            _world = world;
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = sprites.Player1;
            _sr.sortingOrder = 25;
            gameObject.SetActive(false);
        }

        /// <summary>新游戏 / 复活时放到屏幕下方中央。</summary>
        public void Spawn(float invincibleTime, bool keepPowerUps)
        {
            gameObject.SetActive(true);
            IsAlive = true;
            _sr.color = Color.white;
            transform.localScale = Vector3.one;
            transform.position = new Vector3(0f, WorldBounds.Bottom + WorldBounds.Height * 0.18f, 0f);
            _fireTimer = 0f;
            _invincibleTimer = invincibleTime;
            if (!keepPowerUps) SetDoubleBullet(0f);
        }

        public void Hide()
        {
            IsAlive = false;
            gameObject.SetActive(false);
        }

        public void SetDoubleBullet(float duration)
        {
            _doubleTimer = duration;
            _lastDoubleReport = -1f;
            GameEvents.RaiseDoubleBulletChanged(Mathf.Max(0f, duration));
        }

        public void Tick(float dt, Vector2 moveDelta)
        {
            if (!IsAlive) return;

            // —— 移动（相对拖拽），限制在屏幕内
            Vector2 half = _cfg.playerSize * 0.5f;
            Vector2 pos = (Vector2)transform.position + moveDelta;
            pos = WorldBounds.Clamp(pos, new Vector2(half.x * 0.9f, half.y * 0.8f));
            transform.position = new Vector3(pos.x, pos.y, 0f);

            // —— 尾焰两帧动画
            _animTimer += dt;
            if (_animTimer >= 0.1f)
            {
                _animTimer = 0f;
                _flameFrame = !_flameFrame;
                _sr.sprite = _flameFrame ? _sprites.Player2 : _sprites.Player1;
            }

            // —— 无敌闪烁
            if (_invincibleTimer > 0f)
            {
                _invincibleTimer -= dt;
                bool visible = _invincibleTimer <= 0f || Mathf.Repeat(_invincibleTimer, 0.2f) > 0.1f;
                _sr.color = new Color(1f, 1f, 1f, visible ? 1f : 0.35f);
            }

            // —— 双排子弹计时
            if (_doubleTimer > 0f)
            {
                _doubleTimer -= dt;
                if (_doubleTimer <= 0f) SetDoubleBullet(0f);
                else if (Mathf.Abs(_lastDoubleReport - _doubleTimer) >= 1f)
                {
                    _lastDoubleReport = _doubleTimer;
                    GameEvents.RaiseDoubleBulletChanged(_doubleTimer);
                }
            }

            // —— 自动射击
            _fireTimer -= dt;
            if (_fireTimer <= 0f)
            {
                _fireTimer += _cfg.fireInterval;
                if (_fireTimer < 0f) _fireTimer = 0f;
                Fire(pos);
            }
        }

        private void Fire(Vector2 pos)
        {
            float noseY = pos.y + _cfg.playerSize.y * 0.45f;
            if (HasDoubleBullet)
            {
                float dx = _cfg.playerSize.x * 0.17f;
                float y = pos.y + _cfg.playerSize.y * 0.12f;
                _world.SpawnBullet(new Vector2(pos.x - dx, y), true);
                _world.SpawnBullet(new Vector2(pos.x + dx, y), true);
            }
            else
            {
                _world.SpawnBullet(new Vector2(pos.x, noseY), false);
            }
            GameEvents.RaisePlayerFired();
        }

        /// <summary>被撞毁：播放爆炸，隐藏飞机。</summary>
        public void Kill()
        {
            if (!IsAlive) return;
            IsAlive = false;
            _world.SpawnExplosion(transform.position, _cfg.playerSize.x * 1.6f, 0.8f);
            gameObject.SetActive(false);
        }
    }
}
