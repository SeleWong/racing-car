using System.Collections.Generic;
using UnityEngine;

namespace PlaneWar
{
    /// <summary>无限纵向滚动背景：若干张图首尾相接循环，自动适配屏幕尺寸。</summary>
    public class ScrollingBackground : MonoBehaviour
    {
        private readonly List<SpriteRenderer> _tiles = new List<SpriteRenderer>();
        private Sprite _sprite;
        private float _speed;
        private float _tileHeight;
        private float _offset;

        public void Init(Sprite sprite, float speed)
        {
            _sprite = sprite;
            _speed = speed;
            WorldBounds.Changed += Rebuild;
            Rebuild();
        }

        private void OnDestroy() { WorldBounds.Changed -= Rebuild; }

        private void Rebuild()
        {
            if (_sprite == null) return;
            Vector2 spriteSize = _sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            // 横向铺满宽度（等比缩放）
            float scale = WorldBounds.Width / spriteSize.x;
            _tileHeight = spriteSize.y * scale;
            int needed = Mathf.CeilToInt(WorldBounds.Height / _tileHeight) + 1;

            while (_tiles.Count < needed)
            {
                var go = new GameObject("BG_" + _tiles.Count);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sprite;
                sr.sortingOrder = -100;
                _tiles.Add(sr);
            }
            for (int i = 0; i < _tiles.Count; i++)
            {
                _tiles[i].gameObject.SetActive(i < needed);
                _tiles[i].transform.localScale = new Vector3(scale, scale, 1f);
            }
            Layout();
        }

        public void Tick(float dt)
        {
            if (_tileHeight <= 0f) return;
            _offset = Mathf.Repeat(_offset + _speed * dt, _tileHeight);
            Layout();
        }

        private void Layout()
        {
            float y = WorldBounds.Bottom + _tileHeight * 0.5f - _offset;
            for (int i = 0; i < _tiles.Count; i++)
            {
                if (!_tiles[i].gameObject.activeSelf) continue;
                _tiles[i].transform.localPosition = new Vector3(0f, y, 0f);
                y += _tileHeight;
            }
        }
    }
}
