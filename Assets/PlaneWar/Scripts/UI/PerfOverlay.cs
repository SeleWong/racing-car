using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace PlaneWar
{
    /// <summary>
    /// 性能面板：FPS、最差帧耗时、托管堆、GC 次数、对象池与实体数量。
    /// 用于在真机上验证“局内无 GC”——正常游玩时 GC 次数只应在结算 / 主界面时增加。
    /// 面板自身每 0.5 秒刷新一次文本（仅显示时），不会影响测量结论。
    /// </summary>
    public class PerfOverlay : MonoBehaviour
    {
        private GameManager _gm;
        private Text _text;
        private readonly StringBuilder _sb = new StringBuilder(256);
        private float _accum;
        private int _frames;
        private float _worstFrame;
        private float _timer;
        private int _gcAtStart;

        public void Init(GameManager gm, UIFactory f, RectTransform parent, bool visible)
        {
            _gm = gm;
            var bg = f.Image("PerfOverlay", parent, f.Sprites.White, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Place(bg.rectTransform, new Vector2(1f, 1f), new Vector2(-12, -100), new Vector2(300, 190));
            bg.gameObject.AddComponent<Canvas>(); // 独立 Canvas，刷新时不影响主 UI 合批
            _text = f.Text("Text", bg.transform, "", 20, Color.white, TextAnchor.UpperLeft);
            UIFactory.Stretch(_text.rectTransform, 8f);
            _gcAtStart = System.GC.CollectionCount(0);
            SetVisible(visible);
        }

        public void Toggle() { SetVisible(!_text.transform.parent.gameObject.activeSelf); }

        private void SetVisible(bool v)
        {
            _text.transform.parent.gameObject.SetActive(v);
            enabled = v;
            _accum = 0f; _frames = 0; _worstFrame = 0f; _timer = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _accum += dt;
            _frames++;
            if (dt > _worstFrame) _worstFrame = dt;
            _timer += dt;
            if (_timer < 0.5f) return;

            float fps = _frames / Mathf.Max(0.0001f, _accum);
            long heap = System.GC.GetTotalMemory(false);
            var w = _gm.World;

            _sb.Length = 0;
            _sb.Append("FPS ").Append(Mathf.RoundToInt(fps))
               .Append("   最差 ").Append(Mathf.RoundToInt(_worstFrame * 1000f)).Append("ms\n");
            _sb.Append("托管堆 ").Append((heap / (1024f * 1024f)).ToString("F1")).Append(" MB\n");
            _sb.Append("GC(Gen0) ").Append(System.GC.CollectionCount(0) - _gcAtStart).Append('\n');
            _sb.Append("池对象 ").Append(w.PooledObjectCount).Append('\n');
            _sb.Append("子弹 ").Append(w.Bullets.Count)
               .Append("  敌机 ").Append(w.Enemies.Count)
               .Append("  特效 ").Append(w.Explosions.Count).Append('\n');
            _sb.Append(_gm.Platform.Name).Append("  ").Append(Screen.width).Append('x').Append(Screen.height);
            _text.text = _sb.ToString();

            _timer = 0f; _accum = 0f; _frames = 0; _worstFrame = 0f;
        }
    }
}
