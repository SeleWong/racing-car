// ---------------------------------------------------------------------------
// 表现特效池:爆炸碎片、击杀飘分、卷轴星空背景。
// 全部由代码构建(无预制体依赖),挂在游戏 Canvas 的 EntityRoot 下。
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlaneWar.Rendering
{
    // ============================ 爆炸 ============================

    public sealed class ExplosionFx : MonoBehaviour
    {
        private const int ShardCount = 10;
        private readonly Vector2[] _dir = new Vector2[ShardCount];
        private readonly float[] _spd = new float[ShardCount];
        private readonly float[] _size = new float[ShardCount];
        private readonly Image[] _img = new Image[ShardCount];
        private Color _tint;
        private float _radius;
        private float _t = -1f;
        private float _life = 0.5f;

        public void Build(Sprite whiteSprite)
        {
            for (int i = 0; i < ShardCount - 1; i++)
            {
                var go = new GameObject("Shard", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                img.sprite = whiteSprite;
                _img[i] = img;
            }
            // 中心闪光
            var flashGo = new GameObject("Flash", typeof(RectTransform));
            flashGo.transform.SetParent(transform, false);
            var flash = flashGo.AddComponent<Image>();
            flash.raycastTarget = false;
            flash.sprite = whiteSprite;
            _img[ShardCount - 1] = flash;
        }

        public void Play(Vector2 worldPos, float worldRadius, Color tint)
        {
            var rt = (RectTransform)transform;
            rt.anchoredPosition = worldPos * EntityView.PixelsPerUnit;
            _tint = tint;
            _radius = worldRadius * EntityView.PixelsPerUnit;
            _life = 0.42f + worldRadius * 0.07f;
            _t = 0f;
            for (int i = 0; i < ShardCount - 1; i++)
            {
                float ang = (i / (float)(ShardCount - 1)) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                _dir[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                _spd[i] = Random.Range(0.55f, 1.05f);
                _size[i] = Random.Range(0.08f, 0.22f) * worldRadius * EntityView.PixelsPerUnit;
            }
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;
            float k = _t / _life;
            if (k >= 1f)
            {
                _t = -1f;
                gameObject.SetActive(false);
                return;
            }
            float ease = 1f - (1f - k) * (1f - k); // easeOutQuad
            float fade = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
            for (int i = 0; i < ShardCount - 1; i++)
            {
                var img = _img[i];
                var rt = img.rectTransform;
                rt.anchoredPosition = _dir[i] * (_radius * _spd[i] * ease);
                float s = _size[i] * (1f + ease * 0.8f);
                rt.sizeDelta = new Vector2(s, s);
                img.color = new Color(
                    Mathf.Lerp(1f, _tint.r, ease),
                    Mathf.Lerp(1f, _tint.g, ease),
                    Mathf.Lerp(1f, _tint.b, ease),
                    fade * 0.95f);
            }
            // 中心闪光:快速扩散淡出
            var flash = _img[ShardCount - 1];
            var frt = flash.rectTransform;
            float fs = _radius * (0.4f + ease * 1.4f);
            frt.sizeDelta = new Vector2(fs, fs);
            flash.color = new Color(1f, 0.95f, 0.85f, (1f - ease) * 0.9f);
        }
    }

    public sealed class ExplosionPool : MonoBehaviour
    {
        private readonly Queue<ExplosionFx> _free = new Queue<ExplosionFx>();
        private Sprite _white;

        public static ExplosionPool Build(RectTransform root, Sprite white = null)
        {
            var go = new GameObject("ExplosionPool", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var pool = go.AddComponent<ExplosionPool>();
            pool._white = white != null ? white : SpriteFactory.White;
            for (int i = 0; i < 14; i++)
            {
                var fxGo = new GameObject("Explosion", typeof(RectTransform));
                fxGo.transform.SetParent(go.transform, false);
                var fx = fxGo.AddComponent<ExplosionFx>();
                fx.Build(pool._white);
                fxGo.SetActive(false);
                pool._free.Enqueue(fx);
            }
            return pool;
        }

        public void Play(float worldX, float worldY, float worldRadius, Color tint)
        {
            ExplosionFx fx;
            if (_free.Count == 0)
            {
                // 池耗尽:复用最早的(队列尾部不可取,直接新建)
                var fxGo = new GameObject("Explosion", typeof(RectTransform));
                fxGo.transform.SetParent(transform, false);
                fx = fxGo.AddComponent<ExplosionFx>();
                fx.Build(_white);
            }
            else fx = _free.Dequeue();
            fx.Play(new Vector2(worldX, worldY), worldRadius, tint);
        }

        private void Update()
        {
            // 播放完毕的自动回池
            for (int i = 0; i < transform.childCount; i++)
            {
                var fx = transform.GetChild(i).GetComponent<ExplosionFx>();
                if (fx != null && !fx.gameObject.activeSelf && !_free.Contains(fx))
                    _free.Enqueue(fx);
            }
        }
    }

    // ============================ 飘分 ============================

    public sealed class ScorePopupFx : MonoBehaviour
    {
        private Text _text;
        private float _t = -1f;
        private const float Life = 0.85f;

        public void Build()
        {
            _text = gameObject.AddComponent<Text>();
            _text.raycastTarget = false;
            _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.fontSize = 30;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(200f, 44f);
            _text.supportRichText = false;
        }

        public void Play(Vector2 worldPos, string label, Color color)
        {
            ((RectTransform)transform).anchoredPosition = worldPos * EntityView.PixelsPerUnit;
            _text.text = label;
            _text.color = color;
            _t = 0f;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;
            float k = _t / Life;
            if (k >= 1f)
            {
                _t = -1f;
                gameObject.SetActive(false);
                return;
            }
            var rt = (RectTransform)transform;
            rt.anchoredPosition += new Vector2(0f, 90f * Time.deltaTime);
            float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.85f);
            rt.localScale = new Vector3(pop, pop, 1f);
            Color c = _text.color;
            c.a = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
            _text.color = c;
        }
    }

    public sealed class ScorePopupPool : MonoBehaviour
    {
        private readonly Queue<ScorePopupFx> _free = new Queue<ScorePopupFx>();

        public static ScorePopupPool Build(RectTransform root)
        {
            var go = new GameObject("ScorePopupPool", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var pool = go.AddComponent<ScorePopupPool>();
            for (int i = 0; i < 8; i++)
            {
                var fxGo = new GameObject("Popup", typeof(RectTransform));
                fxGo.transform.SetParent(go.transform, false);
                var fx = fxGo.AddComponent<ScorePopupFx>();
                fx.Build();
                fxGo.SetActive(false);
                pool._free.Enqueue(fx);
            }
            return pool;
        }

        public void Play(float worldX, float worldY, string label, Color color)
        {
            ScorePopupFx fx;
            if (_free.Count == 0)
            {
                var fxGo = new GameObject("Popup", typeof(RectTransform));
                fxGo.transform.SetParent(transform, false);
                fx = fxGo.AddComponent<ScorePopupFx>();
                fx.Build();
            }
            else fx = _free.Dequeue();
            fx.Play(new Vector2(worldX, worldY), label, color);
        }

        private void Update()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var fx = transform.GetChild(i).GetComponent<ScorePopupFx>();
                if (fx != null && !fx.gameObject.activeSelf && !_free.Contains(fx))
                    _free.Enqueue(fx);
            }
        }
    }

    // ============================ 卷轴背景 ============================

    public sealed class BackgroundScroller : MonoBehaviour
    {
        /// <summary>背景滚动速度(世界单位/秒)。</summary>
        public const float ScrollSpeed = 2.2f;

        private RectTransform _a, _b;
        private float _offset;
        private const float HeightPx = GameCoreViewConst.WorldHeightPx;

        public static BackgroundScroller Build(RectTransform root, Sprite bgSprite)
        {
            var go = new GameObject("Background", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(900f, 1600f);
            var scroller = go.AddComponent<BackgroundScroller>();
            scroller._a = MakeLayer(go.transform, "LayerA", bgSprite, 0f);
            scroller._b = MakeLayer(go.transform, "LayerB", bgSprite, HeightPx);
            return scroller;
        }

        private static RectTransform MakeLayer(Transform parent, string name, Sprite sprite, float y)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = sprite;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(900f, HeightPx);
            rt.anchoredPosition = new Vector2(0f, y);
            return rt;
        }

        public void ResetScroll()
        {
            _offset = 0f;
            Apply();
        }

        public void Tick(float dt, bool active)
        {
            if (active)
            {
                _offset -= ScrollSpeed * EntityView.PixelsPerUnit * dt;
                if (_offset <= -HeightPx) _offset += HeightPx;
                Apply();
            }
        }

        private void Apply()
        {
            _a.anchoredPosition = new Vector2(0f, _offset);
            _b.anchoredPosition = new Vector2(0f, _offset + HeightPx);
        }
    }

    internal static class GameCoreViewConst
    {
        public const float WorldHeightPx = PlaneWar.Core.GameConfig.WorldHeight * EntityView.PixelsPerUnit;
    }
}
