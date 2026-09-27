using System;
using UnityEngine;

namespace PlaneWar
{
    /// <summary>默认实现：PlayerPrefs 存档，其它能力为空实现。各平台继承后按需覆盖。</summary>
    public class PlatformBase : IPlatformService
    {
        public virtual string Name { get { return "Default"; } }

        public event Action Hidden;
        protected void RaiseHidden() { if (Hidden != null) Hidden(); }

        public virtual int PreferredFrameRate(int configured) { return configured; }

        public virtual void Init(Action onReady) { if (onReady != null) onReady(); }

        public virtual void SaveInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        public virtual int LoadInt(string key, int defaultValue) { return PlayerPrefs.GetInt(key, defaultValue); }

        public virtual void Vibrate(VibrateStrength strength) { }

        public virtual bool SupportsShare { get { return false; } }
        public virtual void Share(string title, int score) { Debug.Log("[PlaneWar] Share: " + title); }

        public virtual bool SupportsRewardedAd { get { return false; } }
        public virtual void ShowRewardedAd(string adUnitId, Action<bool> onFinished) { if (onFinished != null) onFinished(false); }

        public virtual void ReportScore(int score) { }
        public virtual void OnGameStart() { }
        public virtual void OnGameOver(int score) { }

        public virtual bool SupportsQuit { get { return false; } }
        public virtual void Quit() { Application.Quit(); }
    }

    /// <summary>Windows / macOS / Linux 独立包及编辑器。</summary>
    public class StandalonePlatform : PlatformBase
    {
        public override string Name { get { return Application.isEditor ? "Editor" : "Standalone"; } }
        public override bool SupportsQuit { get { return !Application.isEditor; } }

#if UNITY_EDITOR
        // 编辑器中模拟激励视频，方便调试复活流程
        public override bool SupportsRewardedAd { get { return true; } }
        public override void ShowRewardedAd(string adUnitId, Action<bool> onFinished)
        {
            Debug.Log("[PlaneWar] (Editor) 模拟激励视频播放完成");
            if (onFinished != null) onFinished(true);
        }
#endif
    }

    /// <summary>iOS / Android 原生包。广告 / 分享可在子类中接入具体 SDK。</summary>
    public class MobilePlatform : PlatformBase
    {
        public override string Name { get { return Application.platform.ToString(); } }

        public override void Init(Action onReady)
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
            base.Init(onReady);
        }

        public override void Vibrate(VibrateStrength strength)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (strength != VibrateStrength.Light) Handheld.Vibrate();
#endif
        }
    }

    /// <summary>浏览器 WebGL（H5）。</summary>
    public class WebGLPlatform : PlatformBase
    {
        public override string Name { get { return "WebGL"; } }

        // 浏览器中设置 targetFrameRate 会改用 setTimeout 驱动，反而不如 requestAnimationFrame 平滑
        public override int PreferredFrameRate(int configured) { return -1; }
    }
}
