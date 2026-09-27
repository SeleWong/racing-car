# 飞机大战 · 微信经典玩法完整复刻(Unity 2019+)

竖版卷轴射击,完整复刻微信《飞机大战》核心逻辑:**拖拽移动 / 自动射击 / 敌机波次生成 /
碰撞判定 / 计分与难度曲线 / 道具 / 死亡结算**,并内置**权威服务器联机模式**,
支持手机、桌面、浏览器等多端同时接入同一局游戏。

![架构](#架构)

## 特性一览

| 模块 | 说明 |
|---|---|
| 🕹 拖拽移动 | 原版手感:按下→拖动,目标 = 按下时位置 + 位移×灵敏度,速度钳制;手指不遮挡机体 |
| 👾 敌机体系 | 小型/中型/大型/BOSS 四类,波次表驱动,随关卡加速、加射速 |
| 💥 碰撞与得分 | AABB 命中、击杀计分、里程分、击杀飘分、爆炸特效、受击闪屏 |
| 🎁 道具 | 双倍火力(限时双发)、炸弹(清屏)、加血(上限 5 命) |
| 📈 难度曲线 | 每 30 秒一关,8 关内拉满;速度/射速/里程分频率线性插值 |
| 🏁 结算界面 | 得分 / 最高分(新纪录标记)/ 击坠数 / 生存时间 / 重新开始 / 返回主菜单 |
| ⏸ 暂停 | 单机模式暂停覆盖层 |
| 🌐 多端联机 | WebSocket 权威服务器:60Hz 仿真 / 20Hz 快照 / 120ms 插值,客户端只发输入 |
| 🎨 零美术依赖 | 全部贴图运行时程序化绘制并缓存,开箱即跑 |

## 快速开始

### 单机模式

1. 用 **Unity 2019.4 LTS 或更高版本**打开本目录(首次打开会自动导入);
2. 打开场景 `Assets/Scenes/Main.unity`;
3. 点击 Play —— 主菜单点【开始游戏】即可,鼠标/触摸拖拽移动。

> 无需导入任何资源包、无需 TextMesh Pro、无需第三方插件。

### 联机模式(多端接入)

1. 启动权威服务器(需要 .NET 6+):

   ```bash
   cd Server
   dotnet run                 # 默认监听 8080
   dotnet run -- --port 9000  # 自定义端口
   ```

2. 在**每一端**的 Unity 工程里,把场景 `Bootstrap` 物体上 `GameBootstrap` 的
   `Mode` 设为 `Online`,并把 `ServerUrl` 改成服务器地址,例如:
   - 本机联调:`ws://127.0.0.1:8080/`
   - 局域网手机:`ws://192.168.x.x:8080/`
   - 公网部署:`wss://your-host/`(需反向代理终结 TLS)
3. 各端启动后进入【联机模式 · 多端同局】:所有端看到同一局游戏,
   任一端拖拽都会控制同一架战机,任一端在结算界面点【再来一局】全体重开。

> 服务器与客户端**编译同一份** `Assets/Scripts/Core` 源码(见
> `Server/GameServer.csproj` 的 `<Compile Include>`),规则字节级一致。

## 架构

```
┌────────────── Unity 客户端 ──────────────┐      ┌────── GameServer ──────┐
│ UI 面板 ─ GameView(渲染/插值/特效)      │      │ WsServer(HttpListener) │
│        │            │                    │      │        │               │
│   DragInput     GameCore(单机)          │ ws://│        ▼               │
│  (拖拽输入)        │                     │◀────▶│  Room:GameCore 权威    │
│        └── 输入帧/快照/事件 二进制协议 ──┼──────┼─ 60Hz仿真/20Hz快照     │
└──────────────────────────────────────────┘      └────────────────────────┘
                    ▲ 共用:Assets/Scripts/Core(GameConfig/GameCore/序列化)
```

- `Assets/Scripts/Core` —— **纯 C# 确定性仿真内核**(无 UnityEngine 依赖):
  全部游戏规则、数值、二进制序列化;
- `Assets/Scripts/Gameplay` —— 装配与输入(拖拽、相机、启动流程);
- `Assets/Scripts/Rendering` —— 表现层(实体池、爆炸/飘分特效、卷轴背景、
  运行时贴图工厂);
- `Assets/Scripts/UI` —— 主菜单 / HUD / 结算 / 暂停 / 连接面板(代码构建);
- `Assets/Scripts/Net` —— 协议编解码、WebSocket 通道、会话与插值缓冲;
- `Server/` —— 权威服务器(.NET 控制台,零第三方依赖);
- `tools/` —— 工程生成器(make_unity_project.py)、结构校验(check_project.py)、
  符号级交叉检查(check_symbols.py)、玩法仿真测试(sim_gameplay.py)。

完整规则与数值定义见 **[GAME_SPEC.md](GAME_SPEC.md)**。

## 测试与校验

```bash
python3 tools/make_unity_project.py   # 生成/补全 .meta、场景、ProjectSettings(幂等)
python3 tools/check_project.py        # 结构校验:meta 完整性/场景引用/序列化字段
python3 tools/check_symbols.py        # C# 语法(tree-sitter)+ 全工程符号交叉检查
python3 tools/sim_gameplay.py         # 玩法规则仿真测试(数值自动取自 GameConfig)
```

## 平台说明

| 平台 | 单机 | 联机 |
|---|---|---|
| Windows / macOS / Linux | ✅ | ✅ |
| iOS / Android | ✅(触摸拖拽) | ✅ |
| WebGL | ✅ | ⚠️ 需将 `IClientChannel` 换成浏览器 WebSocket 的 JS 桥接(接口已抽象) |

## 操作

- **移动**:按住屏幕/鼠标左键拖动(战机跟随位移,速度有上限,手感与原版一致);
- **射击**:全自动;
- **暂停**:右上角 `II`(单机模式);
- **重开**:结算界面【再来一局】。
