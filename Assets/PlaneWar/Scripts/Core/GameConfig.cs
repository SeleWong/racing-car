using UnityEngine;

namespace PlaneWar
{
    public enum EnemyKind { Small = 0, Medium = 1, Large = 2 }

    public enum SupplyKind { DoubleBullet = 0, Bomb = 1 }

    /// <summary>单种敌机参数（对应原版 enemy1 / enemy2 / enemy3）。</summary>
    [System.Serializable]
    public class EnemyTypeConfig
    {
        public EnemyKind kind;
        [Tooltip("生命值：原版小飞机 1 / 中飞机 8 / 大飞机 20")]
        public int hp = 1;
        [Tooltip("击毁得分：原版 1000 / 6000 / 30000")]
        public int score = 1000;
        [Tooltip("世界单位尺寸（屏幕宽度固定 9 单位）")]
        public Vector2 size = new Vector2(1.07f, 0.8f);
        [Tooltip("碰撞盒相对 size 的比例")]
        public Vector2 hitboxScale = new Vector2(0.8f, 0.8f);
        public float minSpeed = 3.5f;
        public float maxSpeed = 5.5f;
        [Tooltip("基础生成间隔（秒），会被难度系数缩放")]
        public float spawnInterval = 0.9f;
        [Tooltip("开局后首次出现的延迟（秒）")]
        public float firstDelay = 1f;
        [Tooltip("同屏最大数量")]
        public int maxAlive = 12;
        [Tooltip("爆炸动画时长（秒）")]
        public float dieDuration = 0.35f;

        [Header("可选美术替换（留空使用程序生成的图）")]
        public Sprite sprite;
        public Sprite hitSprite;

        public EnemyTypeConfig Clone() { return (EnemyTypeConfig)MemberwiseClone(); }
    }

    /// <summary>难度等级：分数达到阈值后生效。</summary>
    [System.Serializable]
    public class DifficultyLevel
    {
        public int scoreThreshold;
        [Tooltip("生成间隔系数，越小越密集")]
        public float spawnIntervalMultiplier = 1f;
        [Tooltip("敌机速度系数")]
        public float speedMultiplier = 1f;

        public DifficultyLevel() { }

        public DifficultyLevel(int threshold, float interval, float speed)
        {
            scoreThreshold = threshold;
            spawnIntervalMultiplier = interval;
            speedMultiplier = speed;
        }
    }

    /// <summary>
    /// 全局配置。放在任意 Resources 目录下并命名为 PlaneWarConfig 即可被自动加载；
    /// 找不到时使用代码里的默认值（开箱即可运行）。
    /// </summary>
    [CreateAssetMenu(fileName = "PlaneWarConfig", menuName = "PlaneWar/Game Config")]
    public class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "PlaneWarConfig";

        [Header("启动")]
        [Tooltip("场景中没有 GameManager 时自动创建（任何场景点 Play 即可运行）")]
        public bool autoBootstrap = true;
        public int targetFrameRate = 60;

        [Header("世界 / 屏幕适配")]
        [Tooltip("逻辑宽度（世界单位），高度随屏幕比例变化")]
        public float worldWidth = 9f;
        [Tooltip("允许的最大宽高比；更宽的屏幕（PC/平板横屏）左右加黑边")]
        public float maxAspect = 0.75f;
        public Color letterboxColor = Color.black;
        public float backgroundScrollSpeed = 1.2f;

        [Header("玩家")]
        public int playerLives = 1;
        public Vector2 playerSize = new Vector2(1.9f, 2.36f);
        [Tooltip("玩家碰撞盒（比外观小，手感更宽容，与原版一致）")]
        public Vector2 playerHitbox = new Vector2(0.7f, 1.3f);
        public float fireInterval = 0.15f;
        public float bulletSpeed = 18f;
        public Vector2 bulletSize = new Vector2(0.12f, 0.32f);
        public int bulletDamage = 1;
        [Tooltip("双排子弹持续时间（秒）")]
        public float doubleBulletDuration = 18f;
        [Tooltip("开局 / 复活无敌时间（秒）")]
        public float spawnInvincible = 1.5f;
        public float reviveInvincible = 3f;
        public int maxBombs = 3;
        public int startBombs = 0;

        [Header("输入")]
        [Tooltip("拖拽灵敏度，1 = 手指移动多少飞机移动多少")]
        public float dragSensitivity = 1f;
        [Tooltip("键盘 / 手柄移动速度（单位/秒）")]
        public float keyboardSpeed = 11f;
        [Tooltip("双击屏幕释放炸弹（原版 PC 版操作）")]
        public bool doubleTapBomb = true;
        public float doubleTapTime = 0.3f;

        [Header("敌机")]
        public EnemyTypeConfig small = new EnemyTypeConfig
        {
            kind = EnemyKind.Small, hp = 1, score = 1000, size = new Vector2(1.07f, 0.8f),
            hitboxScale = new Vector2(0.85f, 0.8f), minSpeed = 3.5f, maxSpeed = 5.5f,
            spawnInterval = 0.85f, firstDelay = 1f, maxAlive = 14, dieDuration = 0.3f
        };

        public EnemyTypeConfig medium = new EnemyTypeConfig
        {
            kind = EnemyKind.Medium, hp = 8, score = 6000, size = new Vector2(1.3f, 1.86f),
            hitboxScale = new Vector2(0.85f, 0.85f), minSpeed = 2.2f, maxSpeed = 3.2f,
            spawnInterval = 5f, firstDelay = 8f, maxAlive = 4, dieDuration = 0.4f
        };

        public EnemyTypeConfig large = new EnemyTypeConfig
        {
            kind = EnemyKind.Large, hp = 20, score = 30000, size = new Vector2(3.17f, 4.84f),
            hitboxScale = new Vector2(0.85f, 0.9f), minSpeed = 1.2f, maxSpeed = 1.6f,
            spawnInterval = 20f, firstDelay = 25f, maxAlive = 1, dieDuration = 0.6f
        };

        [Header("难度（按分数阈值升序）")]
        public DifficultyLevel[] levels =
        {
            new DifficultyLevel(0, 1.00f, 1.00f),
            new DifficultyLevel(50000, 0.85f, 1.10f),
            new DifficultyLevel(150000, 0.70f, 1.20f),
            new DifficultyLevel(300000, 0.60f, 1.30f),
            new DifficultyLevel(600000, 0.50f, 1.40f),
            new DifficultyLevel(1000000, 0.40f, 1.55f),
        };

        [Header("补给")]
        public float supplyFirstDelay = 15f;
        public float supplyInterval = 30f;
        public float supplyIntervalRandom = 5f;
        [Range(0f, 1f)] public float bombSupplyChance = 0.4f;
        public Vector2 supplySize = new Vector2(1.1f, 1.6f);
        public float supplyEnterSpeed = 4f;
        public float supplyFallSpeed = 9f;

        [Header("平台 / 商业化（可选）")]
        [Tooltip("平台支持激励视频时，游戏结束可看广告复活一次")]
        public bool allowRevive = true;
        public string rewardedAdUnitId = "";
        public string shareTitle = "我在飞机大战中得了 {0} 分，快来挑战我！";

        [Header("性能 / 内存上限（防止异常情况下无限增长）")]
        [Tooltip("同屏子弹上限；超过时本次不再发射")]
        public int maxActiveBullets = 120;
        [Tooltip("同屏爆炸特效上限；超过时跳过特效（不影响逻辑）")]
        public int maxActiveExplosions = 24;
        [Tooltip("射击音效最短间隔（秒）。WebGL / 小游戏每次播放都会创建音频节点，适当调大可降低开销")]
        public float shootSfxMinInterval = 0.15f;
        [Tooltip("显示 FPS / 内存 / GC 次数面板（也可在游戏中按 F1 切换）")]
        public bool showPerfStats = false;

        [Header("可选美术替换（留空使用程序生成）")]
        public Sprite playerSprite;
        public Sprite playerSprite2;
        public Sprite bulletSprite;
        public Sprite doubleBulletSprite;
        public Sprite backgroundSprite;
        public Sprite bulletSupplySprite;
        public Sprite bombSupplySprite;
        public Sprite bombIconSprite;
        [Tooltip("UI 字体。WebGL / 微信小游戏没有系统字体回退，显示中文必须指定一个中文字体")]
        public Font uiFont;

        [Header("可选音效替换（留空使用程序合成音效）")]
        public AudioClip bgm;
        public AudioClip sfxShoot;
        public AudioClip sfxEnemyDownSmall;
        public AudioClip sfxEnemyDownMedium;
        public AudioClip sfxEnemyDownLarge;
        public AudioClip sfxLargeAppear;
        public AudioClip sfxGetDoubleBullet;
        public AudioClip sfxGetBomb;
        public AudioClip sfxUseBomb;
        public AudioClip sfxGameOver;
        public AudioClip sfxButton;

        public EnemyTypeConfig GetEnemy(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Medium: return medium;
                case EnemyKind.Large: return large;
                default: return small;
            }
        }

        /// <summary>是否为运行时用默认值创建的实例（需要由使用者销毁；工程资源绝不能 Destroy）。</summary>
        public bool IsRuntimeInstance { get; private set; }

        public static GameConfig Load()
        {
            var cfg = Resources.Load<GameConfig>(ResourcePath);
            if (cfg == null)
            {
                cfg = CreateInstance<GameConfig>();
                cfg.name = "PlaneWarConfig(Default)";
                cfg.IsRuntimeInstance = true;
            }
            cfg.Validate();
            return cfg;
        }

        public void Validate()
        {
            if (levels == null || levels.Length == 0)
                levels = new[] { new DifficultyLevel(0, 1f, 1f) };
            System.Array.Sort(levels, (a, b) => a.scoreThreshold.CompareTo(b.scoreThreshold));
            small.kind = EnemyKind.Small;
            medium.kind = EnemyKind.Medium;
            large.kind = EnemyKind.Large;
            maxBombs = Mathf.Max(0, maxBombs);
            playerLives = Mathf.Max(1, playerLives);
            maxAspect = Mathf.Clamp(maxAspect, 0.3f, 3f);
            maxActiveBullets = Mathf.Clamp(maxActiveBullets, 8, 1000);
            maxActiveExplosions = Mathf.Clamp(maxActiveExplosions, 1, 200);
            fireInterval = Mathf.Max(0.02f, fireInterval);
            for (int i = 0; i < 3; i++)
            {
                var ec = GetEnemy((EnemyKind)i);
                ec.spawnInterval = Mathf.Max(0.05f, ec.spawnInterval);
                ec.maxAlive = Mathf.Clamp(ec.maxAlive, 0, 100);
            }
        }

        private void OnValidate() { Validate(); }
    }
}
