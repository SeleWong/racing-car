using System;

namespace PlaneWar
{
    public enum VibrateStrength { Light, Medium, Heavy }

    /// <summary>
    /// 平台服务抽象 —— 多端接入的唯一入口。
    /// 玩法代码只依赖该接口；每个渠道（PC / 移动端 / WebGL / 微信小游戏 / 抖音小游戏 / 自研 SDK ……）
    /// 各自实现一份，由 PlatformServices 按编译宏自动选择，也可在启动前手动 Register。
    /// </summary>
    public interface IPlatformService
    {
        string Name { get; }

        /// <summary>SDK 初始化，完成后必须回调 onReady（游戏在回调后才进入主界面）。</summary>
        void Init(Action onReady);

        // —— 存档 ——
        void SaveInt(string key, int value);
        int LoadInt(string key, int defaultValue);

        // —— 震动 ——
        void Vibrate(VibrateStrength strength);

        // —— 分享 ——
        bool SupportsShare { get; }
        void Share(string title, int score);

        // —— 激励视频（复活） ——
        bool SupportsRewardedAd { get; }
        /// <summary>播放激励视频，回调 true 表示完整观看、应发放奖励。</summary>
        void ShowRewardedAd(string adUnitId, Action<bool> onFinished);

        // —— 排行榜 / 统计 ——
        void ReportScore(int score);
        void OnGameStart();
        void OnGameOver(int score);

        /// <summary>是否需要在界面上显示“退出游戏”按钮（仅 PC 独立包）。</summary>
        bool SupportsQuit { get; }
        void Quit();
    }
}
