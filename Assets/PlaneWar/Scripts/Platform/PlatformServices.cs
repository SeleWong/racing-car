using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 平台服务定位器。
    /// 接入新渠道：实现 IPlatformService（建议继承 PlatformBase），在游戏启动前
    /// （如 [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]）调用 PlatformServices.Register(new MyPlatform())。
    /// </summary>
    public static class PlatformServices
    {
        private static IPlatformService _current;

        public static IPlatformService Current
        {
            get
            {
                if (_current == null) _current = CreateDefault();
                return _current;
            }
        }

        public static void Register(IPlatformService service)
        {
            _current = service;
            Debug.Log("[PlaneWar] Platform registered: " + (service != null ? service.Name : "null"));
        }

        public static IPlatformService CreateDefault()
        {
#if WEIXINMINIGAME
            return new WeChatMiniGamePlatform();
#elif UNITY_EDITOR || UNITY_STANDALONE
            return new StandalonePlatform();
#elif UNITY_ANDROID || UNITY_IOS
            return new MobilePlatform();
#elif UNITY_WEBGL
            return new WebGLPlatform();
#else
            return new PlatformBase();
#endif
        }

        public static void ResetStatics() { _current = null; }
    }
}
