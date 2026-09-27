# 飞机大战 · Unity 复刻版（Plane War）

完整复刻微信 5.0「飞机大战」玩法的 Unity 工程，**Unity 2019.4 LTS 及以上**（2020 / 2021 / 2022 / Unity 6 / 团结引擎均可），
**零美术资源依赖**：飞机、子弹、补给、爆炸、背景、UI 图标和音效全部在运行时程序化生成，打开工程点 Play 即可运行；
也可以在配置文件里一键替换成正式素材。

支持平台：**Windows / macOS / Linux、Android、iOS、WebGL（H5）、微信小游戏**，并提供统一的平台接口方便接入其它渠道（抖音、QQ、TapTap、自有 SDK 等）。

---

## 快速开始

1. 用 Unity Hub **打开本仓库根目录**（首次导入会自动生成 `Library/` 和各 `.meta`）。
2. 首次打开时编辑器脚本会自动创建 `Assets/PlaneWar/Scenes/Main.unity`、加入 Build Settings，并切换为竖屏设置。
   也可以随时通过菜单 **PlaneWar → 创建主场景并加入 Build Settings** 重新生成。
3. 点 **Play**。（其实在**任意场景**点 Play 都能跑——`GameBootstrap` 发现场景里没有 `GameManager` 时会自动创建。）
4. Game 视图建议选 `1080x1920 Portrait` 或 `9:16`。

> 输入使用旧版 Input Manager。如果工程启用了新 Input System，请在 *Player Settings → Active Input Handling* 选择 **Input Manager (Old)** 或 **Both**。

---

## 玩法规则（对照原版）

| 项目 | 规则 |
| --- | --- |
| 操作 | 按住屏幕任意位置拖动，飞机按**手指的相对位移**移动（手指不挡飞机），限制在屏幕内 |
| 射击 | 自动连发；拾取蓝色补给后变为**双排子弹**，持续 18 秒 |
| 敌机 | 小飞机 1 血 / 1000 分；中飞机 8 血 / 6000 分；大飞机 20 血 / 30000 分 |
| 受击 | 中、大飞机被击中时闪白；击毁时播放爆炸动画后消失 |
| 炸弹 | 拾取红色补给获得炸弹（最多 3 个），点击左下角炸弹或**双击屏幕**清除屏幕内所有敌机（照常计分）；数量为 0 时按钮隐藏 |
| 补给 | 约每 30 秒空投一次：缓慢降落 → 回弹 → 快速坠落 |
| 碰撞 | 子弹打敌机扣血；敌机撞到玩家 → 玩家坠毁、敌机同时爆炸；玩家碰撞盒比外观小（与原版手感一致） |
| 难度 | 分数达到 5 万 / 15 万 / 30 万 / 60 万 / 100 万时，敌机更密、更快 |
| 暂停 | 左上角暂停按钮 / Esc / P / Android 返回键；切后台、来电、PC 切换窗口时自动暂停 |
| 结束 | 结算面板显示「飞机大战分数」、最高分、新纪录标记；可重新开始 / 回到主页 / 分享 / 看视频复活（平台支持时，每局一次） |

以上数值都在 `GameConfig` 中可调。

### 各端操作

| 平台 | 移动 | 炸弹 | 暂停 |
| --- | --- | --- | --- |
| 手机 / 小游戏 / 触屏 | 手指拖动 | 双击屏幕 / 炸弹按钮 | 暂停按钮、返回键 |
| PC / Web | 鼠标拖动、方向键、WASD | 空格、B、双击、炸弹按钮 | Esc、P、暂停按钮 |
| 手柄 | 左摇杆 | A 键（JoystickButton0） | Start 键（JoystickButton7） |

---

## 工程结构

```
Assets/PlaneWar/
├── Scripts/
│   ├── Core/        GameManager（入口+状态机）、GameConfig（全部参数）、GameEvents（事件总线）、
│   │                GameBootstrap（零配置启动）、WorldBounds（屏幕适配）、ObjectPool、HitBox
│   ├── Gameplay/    Player、Enemy、Bullet、Supply、Explosion、EnemySpawner/SupplySpawner、
│   │                GameWorld（对象池+统一更新+碰撞）、ScrollingBackground
│   ├── Input/       IInputSource、PointerDragInput（触屏+鼠标）、KeyboardInput（键盘+手柄）、InputRouter
│   ├── Platform/    IPlatformService、PlatformBase / Standalone / Mobile / WebGL、
│   │                WeChatMiniGamePlatform、PlatformServices（按平台自动选择）
│   ├── UI/          UIManager（主界面/HUD/暂停/结算，纯代码构建）、UIFactory、SafeAreaFitter
│   ├── Audio/       AudioManager + SfxSynth（程序合成音效）
│   └── Art/         PixelCanvas（CPU 光栅器）、SpriteLibrary（程序化生成全部图片）
├── Editor/          PlaneWarEditorMenu（一键建场景 / 配置 / 竖屏设置 / 微信宏）
└── Resources/       （可选）PlaneWarConfig.asset
```

### 设计要点

- **状态机**：`Home → Playing ⇄ Paused → PlayerDying → GameOver`。暂停就是不再推进逻辑，不修改 `Time.timeScale`，UI 动画不受影响。
- **统一更新**：所有实体由 `GameManager.Update` → `GameWorld.TickEntities` 按固定顺序推进，然后做碰撞，时序确定，易于调试。
- **自研 AABB 碰撞**：飞机大战里所有物体都是轴对齐的，不依赖 Physics2D，在 WebGL / 小游戏上更省、更可控。
- **对象池**：子弹、敌机、补给、爆炸全部复用，局内零 `Instantiate`，减轻小游戏平台的 GC 压力。
- **事件解耦**：逻辑层只发 `GameEvents`，UI、音效、平台各自订阅；替换其中任意一层都不影响其它部分。
- **屏幕适配**：逻辑宽度固定为 9 个世界单位，高度随屏幕比例变化；宽高比超过 `maxAspect`（PC、平板、横屏网页）时左右加黑边，UI 使用 *Screen Space – Camera* 跟随游戏视口，并用 `SafeAreaFitter` 避开刘海和 Home 指示条。

---

## 多端接入

### 平台服务接口

玩法代码只依赖 `IPlatformService`：

```csharp
public interface IPlatformService
{
    void Init(Action onReady);                 // SDK 初始化
    void SaveInt(string key, int value);       // 存档（最高分、静音）
    int  LoadInt(string key, int defaultValue);
    void Vibrate(VibrateStrength strength);    // 震动
    bool SupportsShare { get; }  void Share(string title, int score);
    bool SupportsRewardedAd { get; }  void ShowRewardedAd(string adUnitId, Action<bool> onFinished);
    void ReportScore(int score);               // 排行榜
    void OnGameStart();  void OnGameOver(int score);   // 统计埋点
    bool SupportsQuit { get; }  void Quit();
}
```

`PlatformServices` 按编译宏自动选择实现：

| 宏 | 实现 | 说明 |
| --- | --- | --- |
| `WEIXINMINIGAME` | `WeChatMiniGamePlatform` | 微信存储、震动、分享、激励视频复活 |
| `UNITY_EDITOR` / `UNITY_STANDALONE` | `StandalonePlatform` | PlayerPrefs；独立包显示「退出游戏」；编辑器里模拟激励视频，方便调试复活流程 |
| `UNITY_ANDROID` / `UNITY_IOS` | `MobilePlatform` | 常亮、竖屏、`Handheld.Vibrate` |
| `UNITY_WEBGL` | `WebGLPlatform` | 浏览器 H5 |

### 接入新渠道（如抖音小游戏、自研 SDK）

```csharp
public class DouyinPlatform : PlaneWar.PlatformBase
{
    public override string Name => "Douyin";
    public override void Init(System.Action onReady) { /* StarkSDK 初始化 */ onReady(); }
    public override bool SupportsRewardedAd => true;
    public override void ShowRewardedAd(string id, System.Action<bool> done) { /* 调用 SDK 广告 */ }
}

static class DouyinEntry
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => PlaneWar.PlatformServices.Register(new DouyinPlatform());
}
```

### 接入新的输入方式

实现 `IInputSource`（输出本帧位移 / 炸弹 / 暂停），然后调用 `GameManager.Instance.Input.Register(mySource)`，会与现有的触屏、键盘、手柄输入叠加生效。

### 微信小游戏发布步骤

1. 导入微信官方转换插件 [minigame-unity-webgl-transform](https://github.com/wechat-miniprogram/minigame-unity-webgl-transform)，切换到 WebGL 平台。
2. 菜单 **PlaneWar → 微信小游戏：添加 WEIXINMINIGAME 宏**（团结引擎的微信小游戏平台会自动定义此宏）。
3. **必须指定中文字体**：WebGL / 小游戏没有系统字体回退，把一个中文 TTF 拖到 `GameConfig.uiFont`（菜单 **PlaneWar → 创建配置文件** 可生成配置）。
4. （可选）在 `GameConfig.rewardedAdUnitId` 填写激励视频广告位 ID，结算界面才会出现「看视频复活」按钮。
5. 用插件的「转换小游戏」导出，在微信开发者工具中打开。

### 其它平台注意事项

- **Android / iOS**：菜单 **PlaneWar → 应用竖屏 & 移动端推荐设置**；刘海屏已通过 SafeArea 自动适配。
- **WebGL**：同样需要设置中文字体；窗口尺寸默认 540×960，任意尺寸都能正确适配。
- **PC**：窗口可自由拉伸，横向多出的部分自动加黑边。

---

## 替换正式美术 / 音效

菜单 **PlaneWar → 创建配置文件** 生成 `Assets/PlaneWar/Resources/PlaneWarConfig.asset`，在 Inspector 中：

- 拖入 `playerSprite / playerSprite2`（尾焰两帧）、`bulletSprite`、各敌机的 `sprite / hitSprite`、`backgroundSprite`、补给和炸弹图标；
- 拖入 `bgm`、`sfxShoot`、`sfxEnemyDown*`、`sfxUseBomb`、`sfxGameOver` 等音效；
- 同时可以调整所有数值：血量、分数、速度、生成间隔、难度曲线、补给频率、双排子弹时长、炸弹上限、生命数、拖拽灵敏度……

留空的项目自动使用程序生成的资源。注意：替换 Sprite 后请同步调整对应的 `size`（世界单位，屏幕宽 = 9）。
