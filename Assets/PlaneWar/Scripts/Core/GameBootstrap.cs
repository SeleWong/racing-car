using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 零配置启动：场景里没有 GameManager 时自动创建，任何场景（包括空场景）点 Play 即可运行。
    /// 可在 GameConfig.autoBootstrap 关闭。
    /// </summary>
    public static class GameBootstrap
    {
#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetStatics()
        {
            // 支持关闭 Domain Reload（Enter Play Mode Options）
            GameEvents.ClearAll();
            WorldBounds.ClearEvents();
            PlatformServices.ResetStatics();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (Object.FindObjectOfType<GameManager>() != null) return;
            var cfg = GameConfig.Load();
            if (!cfg.autoBootstrap) return;
            // 先禁用再挂组件，把已加载的配置传进去，避免 Awake 中重复创建默认配置
            var go = new GameObject("PlaneWar");
            go.SetActive(false);
            var gm = go.AddComponent<GameManager>();
            gm.config = cfg;
            go.SetActive(true);
        }
    }
}
