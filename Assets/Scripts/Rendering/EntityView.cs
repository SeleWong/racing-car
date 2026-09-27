// ---------------------------------------------------------------------------
// 实体视图基类与派生视图 —— 纯表现层,只读逻辑状态,不写逻辑。
// 世界坐标 → Canvas 像素:1 世界单位 = 100 px(参考分辨率 900×1600)。
// ---------------------------------------------------------------------------
using PlaneWar.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PlaneWar.Rendering
{
    public class EntityView : MonoBehaviour
    {
        public const float PixelsPerUnit = 100f;

        public RectTransform Rt;
        public Image Img;

        public int EntityId;
        public bool InUse;

        protected virtual void Awake()
        {
            if (Rt == null) Rt = GetComponent<RectTransform>();
            if (Img == null) Img = GetComponent<Image>();
        }

        /// <summary>设置贴图并按世界尺寸缩放(世界单位→像素)。</summary>
        public void SetSprite(Sprite sprite, float worldW, float worldH)
        {
            Img.sprite = sprite;
            Rt.sizeDelta = new Vector2(worldW * PixelsPerUnit, worldH * PixelsPerUnit);
        }

        public void SetWorldPos(float x, float y)
        {
            Rt.anchoredPosition = new Vector2(x * PixelsPerUnit, y * PixelsPerUnit);
        }

        public virtual void OnRecycle()
        {
            InUse = false;
            EntityId = 0;
            gameObject.SetActive(false);
        }

        public virtual void OnSpawn(int id)
        {
            EntityId = id;
            InUse = true;
            gameObject.SetActive(true);
        }
    }

    /// <summary>玩家战机视图:含死亡坠落的渐隐。</summary>
    public sealed class PlayerView : EntityView
    {
        public float DeathAnimT = -1f; // >=0 表示正在播放死亡动画

        public void PlayNormal()
        {
            DeathAnimT = -1f;
            Img.color = Color.white;
        }

        /// <summary>按死亡流程进度(0→1)播放坠落淡出。</summary>
        public void PlayDeath(float progress)
        {
            DeathAnimT = progress;
            float fade = 1f - Mathf.Clamp01(progress);
            Img.color = new Color(1f, 1f, 1f, fade);
            Rt.localRotation = Quaternion.Euler(0, 0, -progress * 55f);
            Rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.75f, progress);
        }

        public override void OnRecycle()
        {
            base.OnRecycle();
            Rt.localRotation = Quaternion.identity;
            Rt.localScale = Vector3.one;
            Img.color = Color.white;
        }
    }

    /// <summary>敌机视图:受击白闪 + 大型机/BOSS 血条。</summary>
    public sealed class EnemyView : EntityView
    {
        public EnemyKind Kind;
        public Image FlashOverlay;
        public RectTransform HpBarFill;
        private float _flash;

        public void Setup(EnemyKind kind, Sprite sprite, float worldW, float worldH)
        {
            Kind = kind;
            SetSprite(sprite, worldW, worldH);
            if (FlashOverlay != null) FlashOverlay.sprite = sprite; // 白闪只覆盖机体轮廓
            bool showHp = kind == EnemyKind.Large || kind == EnemyKind.Boss;
            if (HpBarFill != null) HpBarFill.parent.gameObject.SetActive(showHp);
        }

        public void SetHp(float ratio)
        {
            if (HpBarFill != null) HpBarFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        public void SetFlash(float timeLeft)
        {
            _flash = timeLeft;
            ApplyFlash();
        }

        private void Update()
        {
            if (_flash > 0f)
            {
                _flash -= Time.deltaTime;
                ApplyFlash();
            }
        }

        private void ApplyFlash()
        {
            if (FlashOverlay == null) return;
            FlashOverlay.color = new Color(1f, 1f, 1f,
                Mathf.Clamp01(_flash / GameConfig.EnemyHitFlashTime) * 0.85f);
        }
    }

    /// <summary>道具视图:呼吸缩放 + 缓慢自旋。</summary>
    public sealed class PowerupView : EntityView
    {
        private float _phase;

        public override void OnSpawn(int id)
        {
            base.OnSpawn(id);
            _phase = Random.value * 6.2831853f;
        }

        private void Update()
        {
            _phase += Time.deltaTime;
            float s = 1f + Mathf.Sin(_phase * 5f) * 0.08f;
            Rt.localScale = new Vector3(s, s, 1f);
            Rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_phase * 1.7f) * 8f);
        }

        public override void OnRecycle()
        {
            base.OnRecycle();
            Rt.localScale = Vector3.one;
            Rt.localRotation = Quaternion.identity;
        }
    }
}
