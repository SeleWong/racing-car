// 微信小游戏适配（基于官方 minigame-unity-webgl-transform 转换插件）
// 使用方式：导入微信转换 SDK 后，在 Player Settings > Scripting Define Symbols 中添加 WEIXINMINIGAME
// （团结引擎的微信小游戏平台会自动定义该宏）。
#if WEIXINMINIGAME
using System;
using UnityEngine;
using WeChatWASM;

namespace PlaneWar
{
    public class WeChatMiniGamePlatform : PlatformBase
    {
        private WXRewardedVideoAd _ad;
        private string _adUnitId;
        private Action<bool> _adCallback;

        public override string Name { get { return "WeChatMiniGame"; } }

        public override void Init(Action onReady)
        {
            WX.InitSDK(code =>
            {
                // 小游戏切后台（来电、下拉通知、切换聊天）时自动暂停
                WX.OnHide(res => RaiseHidden());
                if (onReady != null) onReady();
            });
        }

        public override void SaveInt(string key, int value) { WX.StorageSetIntSync(key, value); }
        public override int LoadInt(string key, int defaultValue) { return WX.StorageGetIntSync(key, defaultValue); }

        public override void Vibrate(VibrateStrength strength)
        {
            string type = strength == VibrateStrength.Light ? "light"
                        : strength == VibrateStrength.Heavy ? "heavy" : "medium";
            WX.VibrateShort(new VibrateShortOption { type = type });
        }

        public override bool SupportsShare { get { return true; } }
        public override void Share(string title, int score)
        {
            WX.ShareAppMessage(new ShareAppMessageOption { title = title });
        }

        public override bool SupportsRewardedAd { get { return true; } }
        public override void ShowRewardedAd(string adUnitId, Action<bool> onFinished)
        {
            if (string.IsNullOrEmpty(adUnitId)) { if (onFinished != null) onFinished(false); return; }
            _adCallback = onFinished;
            if (_ad == null || _adUnitId != adUnitId)
            {
                if (_ad != null) _ad.Destroy(); // 释放旧广告实例及其回调，避免泄漏
                _adUnitId = adUnitId;
                _ad = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam { adUnitId = adUnitId });
                // 官方建议：res 为 null（低版本基础库）时也视为播放完成
                _ad.OnClose(res => Finish(res == null || res.isEnded));
                _ad.OnError(err => Finish(false));
                _ad.Load();
            }
            _ad.Show();
        }

        private void Finish(bool success)
        {
            var cb = _adCallback;
            _adCallback = null;
            if (cb != null) cb(success);
        }

        public override void ReportScore(int score)
        {
            // 好友排行榜需要开放数据域（Open Data Context），在此上报托管数据：
            // WX.SetUserCloudStorage(...)。项目未启用开放数据域时保持空实现。
        }
    }
}
#endif
