using UnityEngine;

namespace PlaneWar
{
    public class Bullet : MonoBehaviour
    {
        public int Damage { get; private set; }
        public Rect Hitbox { get { return HitBox.FromCenter(transform.position, _size); } }

        private SpriteRenderer _sr;
        private float _speed;
        private Vector2 _size;

        public static Bullet Create()
        {
            var go = new GameObject("Bullet");
            var b = go.AddComponent<Bullet>();
            b._sr = go.AddComponent<SpriteRenderer>();
            b._sr.sortingOrder = 20;
            return b;
        }

        public void Setup(Sprite sprite, Vector2 pos, float speed, Vector2 size, int damage)
        {
            _sr.sprite = sprite;
            transform.position = new Vector3(pos.x, pos.y, 0f);
            _speed = speed;
            _size = size;
            Damage = damage;
        }

        /// <returns>false = 飞出屏幕，需要回收</returns>
        public bool Tick(float dt)
        {
            Vector3 p = transform.position;
            p.y += _speed * dt;
            transform.position = p;
            return p.y - _size.y < WorldBounds.Top;
        }
    }
}
