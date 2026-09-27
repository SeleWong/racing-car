// ---------------------------------------------------------------------------
// GameBootstrap —— 启动装配:
//   1. 等待程序化贴图就绪;
//   2. 构建 UI、注入引用、订阅面板事件;
//   3. 按配置进入单机 / 联机模式。
// 挂在场景中的空物体上;所有引用自动查找,无需手动连线。
// ---------------------------------------------------------------------------
using System.Collections;
using PlaneWar.Core;
using PlaneWar.Rendering;
using PlaneWar.UI;
using UnityEngine;

namespace PlaneWar.Gameplay
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public enum StartMode { Local, Online }

        [Tooltip("启动时进入的模式(也可在主菜单切换)")]
        public StartMode Mode = StartMode.Local;

        [Tooltip("联机服务器 WebSocket 地址(多端填同一台机器即可同局)")]
        public string ServerUrl = ClientConfig.DefaultServerUrl;

        [Tooltip("本地随机种子(联机模式由服务器决定)")]
        public uint Seed = 20260927;

        private UIManager _ui;
        private GameView _view;
        private DragInput _drag;

        private IEnumerator Start()
        {
            _ui = FindObjectOfType<UIManager>();
            _view = FindObjectOfType<GameView>();
            _drag = FindObjectOfType<DragInput>();
            if (_ui == null || _view == null || _drag == null)
            {
                Debug.LogError("[Bootstrap] 场景缺少 UIManager / GameView / DragInput。");
                yield break;
            }

            // 程序化贴图(首次生成约数百毫秒,随后读缓存)
            _view.gameObject.SetActive(false); // 等贴图就绪再驱动
            yield return StartCoroutine(SpriteFactory.Prepare(OnSpritesReady));
        }

        private void OnSpritesReady()
        {
            _ui.Build();

            var cam = Camera.main;
            _view.WorldCamera = cam;
            _view.Drag = _drag;
            _view.Ui = _ui;
            _view.gameObject.SetActive(true);

            _drag.WorldCamera = cam;
            _drag.GetAnchorWorld = () => _view.GetPlayerAnchor();

            _ui.OnStartLocal += OnStartLocal;
            _ui.OnStartOnline += OnStartOnline;
            _ui.OnRestart += () => _view.BeginNewGame();
            _ui.OnBackToMenu += () => _view.BackToMenu();
            _ui.OnResume += () => _view.Resume();
            _ui.OnPauseToggle += () => _view.TogglePause();
            _ui.OnCancelOnline += () => _view.CancelOnline();

            if (Mode == StartMode.Online) _view.InitOnline(ServerUrl);
            else _view.InitLocal(Seed);
        }

        private void OnStartLocal()
        {
            if (_view.OnlineMode) _view.BackToMenu(); // 从联机回本地
            _view.BeginNewGame();
        }

        private void OnStartOnline()
        {
            _view.InitOnline(ServerUrl);
        }
    }
}
