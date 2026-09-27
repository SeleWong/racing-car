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
            var go = new GameObject("PlaneWar");
            go.AddComponent<GameManager>();
        }
    }
}
