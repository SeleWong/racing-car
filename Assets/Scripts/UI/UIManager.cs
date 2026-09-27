// ---------------------------------------------------------------------------
// UIManager —— 面板状态机与 HUD 数据入口。
// 面板层级由 UIFactory 在运行时构建(场景零 UI 负担),本组件挂在 Canvas 上。
// 交互事件向上抛给 GameBootstrap/GameView,面板不直接触碰游戏逻辑。
// ---------------------------------------------------------------------------
using PlaneWar.Core;
using UnityEngine;

namespace PlaneWar.UI
{
    public sealed class UIManager : MonoBehaviour
    {
        public MenuPanel Menu { get; private set; }
        public HudPanel Hud { get; private set; }
        public GameOverPanel GameOver { get; private set; }
        public ConnectingPanel Connecting { get; private set; }
        public PauseOverlay Pause { get; private set; }

        // ---- 交互事件(GameBootstrap 订阅) ----
        public event System.Action OnStartLocal;
        public event System.Action OnStartOnline;
        public event System.Action OnRestart;
        public event System.Action OnBackToMenu;
        public event System.Action OnResume;
        public event System.Action OnPauseToggle;
        public event System.Action OnCancelOnline;

        /// <summary>由 GameBootstrap 在贴图准备完成后调用(面板需要白色基础贴图)。</summary>
        public void Build()
        {
            if (Menu != null) return;
            var canvasRt = (RectTransform)transform;
            Menu = UIFactory.BuildMenu(canvasRt, this);
            Hud = UIFactory.BuildHud(canvasRt, this);
            GameOver = UIFactory.BuildGameOver(canvasRt, this);
            Connecting = UIFactory.BuildConnecting(canvasRt, this);
            Pause = UIFactory.BuildPauseOverlay(canvasRt, this);
            ShowPauseOverlay(false);
            Connecting.gameObject.SetActive(false);
            GameOver.gameObject.SetActive(false);
        }

        internal void RaiseStartLocal() { if (OnStartLocal != null) OnStartLocal(); }
        internal void RaiseStartOnline() { if (OnStartOnline != null) OnStartOnline(); }
        internal void RaiseRestart() { if (OnRestart != null) OnRestart(); }
        internal void RaiseBackToMenu() { if (OnBackToMenu != null) OnBackToMenu(); }
        internal void RaiseResume() { if (OnResume != null) OnResume(); }
        internal void RaisePauseToggle() { if (OnPauseToggle != null) OnPauseToggle(); }
        internal void RaiseCancelOnline() { if (OnCancelOnline != null) OnCancelOnline(); }

        // ---- 面板状态机 ----

        public void SetPhase(GamePhase phase, bool online)
        {
            switch (phase)
            {
                case GamePhase.Ready:
                    Menu.gameObject.SetActive(true);
                    Hud.gameObject.SetActive(false);
                    GameOver.gameObject.SetActive(false);
                    ShowPauseOverlay(false);
                    break;
                case GamePhase.Playing:
                    Menu.gameObject.SetActive(false);
                    GameOver.gameObject.SetActive(false);
                    Hud.gameObject.SetActive(true);
                    Hud.SetPauseVisible(!online);
                    break;
                case GamePhase.GameOver:
                    Hud.gameObject.SetActive(false);
                    GameOver.gameObject.SetActive(true);
                    ShowPauseOverlay(false);
                    break;
            }
        }

        public void UpdateMenuHigh(int high)
        {
            Menu.SetHighScore(high);
        }

        public void UpdateHud(int score, int lives, float doubleRatio, bool paused)
        {
            Hud.Render(score, lives, doubleRatio);
        }

        public void ShowGameOver(int score, int high, int kills, float time, bool newRecord)
        {
            GameOver.Render(score, high, newRecord, kills, time);
        }

        public void ShowConnecting(bool show, string error)
        {
            if (show)
            {
                Menu.gameObject.SetActive(false);
                Connecting.gameObject.SetActive(true);
            }
            else
            {
                Connecting.gameObject.SetActive(false);
            }
            if (error != null && Menu != null) Menu.SetError(error);
        }

        public void ShowPauseOverlay(bool show)
        {
            Pause.gameObject.SetActive(show);
        }
    }
}
