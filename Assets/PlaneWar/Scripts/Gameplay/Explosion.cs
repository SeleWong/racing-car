using UnityEngine;

namespace PlaneWar
{
    /// <summary>逐帧爆炸动画。</summary>
    public class Explosion : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _duration;
        private float _time;

        public static Explosion Create()
        {
            var go = new GameObject("Explosion");
            var e = go.AddComponent<Explosion>();
            e._sr = go.AddComponent<SpriteRenderer>();
            e._sr.sortingOrder = 30;
            return e;
        }

        public void Play(Sprite[] frames, Vector2 pos, float size, float duration)
        {
            _frames = frames;
            _duration = Mathf.Max(0.05f, duration);
            _time = 0f;
            transform.position = new Vector3(pos.x, pos.y, 0f);
            transform.localScale = Vector3.one * size; // 帧图世界尺寸为 1
            transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            _sr.sprite = frames[0];
        }

        public bool Tick(float dt)
        {
            _time += dt;
            if (_time >= _duration) return false;
            int idx = Mathf.Min(_frames.Length - 1, (int)(_time / _duration * _frames.Length));
            _sr.sprite = _frames[idx];
            return true;
        }
    }
}
