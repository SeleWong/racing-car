// ---------------------------------------------------------------------------
// ClientConfig —— 客户端运行时配置(联机服务器地址等)。
// 由 GameBootstrap 启动时读取;可在构建后按平台覆盖:
//   · 编辑器/桌面:默认指向本机服务器,可改;
//   · 手机/浏览器:改成部署了 GameServer 的机器地址,如
//     ws://192.168.1.100:8080/ 或 wss://your-host/
// ---------------------------------------------------------------------------

namespace PlaneWar.Gameplay
{
    public static class ClientConfig
    {
        /// <summary>联机模式默认服务器地址(多端接入同一服务器即可同局游戏)。</summary>
        public const string DefaultServerUrl = "ws://127.0.0.1:8080/";
    }
}
