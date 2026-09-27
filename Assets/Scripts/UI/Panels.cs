// ---------------------------------------------------------------------------
// 面板组件:主菜单 / HUD / 结算 / 连接中 / 暂停。
// 仅持有引用与纯表现方法;按钮回调抛给 UIManager 的事件。
// ---------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

namespace PlaneWar.UI
{
    // ============================ 主菜单 ============================

    public sealed class MenuPanel : MonoBehaviour
    {
        public Text Title, Subtitle, HighScore, ErrorText;
        public Button StartButton, OnlineButton;
        public Text StartLabel, OnlineLabel;

        public void SetHighScore(int high)
        {
            HighScore.text = high > 0 ? "High Score: " + high : "High Score: --";
        }

        public void SetError(string error)
        {
            ErrorText.text = error == null ? "" : error;
        }
    }

    // ============================ HUD ============================

    public sealed class HudPanel : MonoBehaviour
    {
        public Text Score;
        public Image[] Hearts;
        public RectTransform DoubleBarRoot;
        public Image DoubleFill;
        public Button PauseButton;

        public void Render(int score, int lives, float doubleRatio)
        {
            Score.text = score.ToString("0");
            for (int i = 0; i < Hearts.Length; i++)
            {
                Hearts[i].color = i < lives
                    ? new Color(1f, 0.55f, 0.6f)
                    : new Color(1f, 1f, 1f, 0.12f);
            }
            bool showBar = doubleRatio > 0f;
            DoubleBarRoot.gameObject.SetActive(showBar);
            if (showBar)
            {
                DoubleFill.anchorMax = new Vector2(Mathf.Clamp01(doubleRatio), 1f);
            }
        }

        public void SetPauseVisible(bool visible)
        {
            PauseButton.gameObject.SetActive(visible);
        }
    }

    // ============================ 结算 ============================

    public sealed class GameOverPanel : MonoBehaviour
    {
        public Text Title, Score, HighScore, NewRecord, Kills, TimeSurvived;
        public Button RestartButton, MenuButton;

        public void Render(int score, int high, bool newRecord, int kills, float time)
        {
            Score.text = score.ToString("0");
            HighScore.text = "High Score " + high.ToString("0");
            NewRecord.gameObject.SetActive(newRecord && score > 0);
            Kills.text = "Kills: " + kills;
            int m = Mathf.FloorToInt(time / 60f);
            int s = Mathf.FloorToInt(time % 60f);
            TimeSurvived.text = string.Format("Survival Time: {0:00}:{1:00}", m, s);
        }
    }

    // ============================ 连接中 ============================

    public sealed class ConnectingPanel : MonoBehaviour
    {
        public Text Status;
        public Button CancelButton;
        private float _t;

        private void OnEnable()
        {
            _t = 0f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            int dots = Mathf.FloorToInt(_t * 2.5f) % 4;
            Status.text = "Connecting to server" + new string('.', dots);
        }
    }

    // ============================ 暂停 ============================

    public sealed class PauseOverlay : MonoBehaviour
    {
        public Button ResumeButton, RestartButton, MenuButton;
    }
}
