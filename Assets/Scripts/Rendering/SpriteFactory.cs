// ---------------------------------------------------------------------------
// SpriteFactory —— 运行时程序化绘制全部游戏贴图(零美术资源依赖)。
// 首次启动绘制后以 PNG 缓存到 StreamingAssets,之后直接读缓存。
// 所有绘制基于矢量图元(三角/椭圆/圆角矩形/辉光)在浮点画布上做
// 抗锯齿混合,输出 Texture2D + Sprite(100 PPU,中心锚点)。
// ---------------------------------------------------------------------------
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PlaneWar.Rendering
{
    public static class SpriteFactory
    {
        public static Sprite Player;
        public static Sprite EnemySmall;
        public static Sprite EnemyMedium;
        public static Sprite EnemyLarge;
        public static Sprite EnemyBoss;
        public static Sprite PlayerBullet;
        public static Sprite EnemyBullet;
        public static Sprite PowerDouble;
        public static Sprite PowerBomb;
        public static Sprite PowerLife;
        public static Sprite Background;
        public static Sprite White;

        public static bool Ready { get; private set; }

        private const string CacheFolder = "Sprites";

        private static string CachePath(string name)
        {
            return Path.Combine(Application.streamingAssetsPath, CacheFolder, name + ".png");
        }

        /// <summary>协程:逐个生成/加载贴图,完成后回调。</summary>
        public static IEnumerator Prepare(Action onComplete)
        {
            var jobs = new (string name, int size, Action<PixelCanvas> draw, Action<Sprite> assign)[]
            {
                ("player",       128, DrawPlayer,       s => Player = s),
                ("enemy_small",   96, DrawEnemySmall,   s => EnemySmall = s),
                ("enemy_medium", 128, DrawEnemyMedium,  s => EnemyMedium = s),
                ("enemy_large",  192, DrawEnemyLarge,   s => EnemyLarge = s),
                ("enemy_boss",   256, DrawEnemyBoss,    s => EnemyBoss = s),
                ("bullet_player", 64, DrawPlayerBullet, s => PlayerBullet = s),
                ("bullet_enemy",  64, DrawEnemyBullet,  s => EnemyBullet = s),
                ("power_double",  96, DrawPowerDouble,  s => PowerDouble = s),
                ("power_bomb",    96, DrawPowerBomb,    s => PowerBomb = s),
                ("power_life",    96, DrawPowerLife,    s => PowerLife = s),
                ("background",   512, DrawBackground,   s => Background = s),
                ("white",          8, DrawWhite,        s => White = s),
            };

            foreach (var job in jobs)
            {
                Sprite sprite = TryLoadFromCache(job.name);
                if (sprite == null)
                {
                    var canvas = new PixelCanvas(job.size, job.size);
                    job.draw(canvas);
                    var tex = canvas.ToTexture(job.name);
                    sprite = MakeSprite(tex);
                    SaveToCache(job.name, tex);
                }
                job.assign(sprite);
                yield return null; // 分摊到多帧,避免启动卡顿
            }
            Ready = true;
            if (onComplete != null) onComplete();
        }

        private static Sprite TryLoadFromCache(string name)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return null; // WebGL 无文件系统缓存,每次内存重建
#else
            string path = CachePath(name);
            if (!File.Exists(path)) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (ImageConversion.LoadImage(tex, File.ReadAllBytes(path))) return MakeSprite(tex);
                UnityEngine.Object.Destroy(tex);
            }
            catch { /* 缓存损坏则重绘 */ }
            return null;
#endif
        }

        private static void SaveToCache(string name, Texture2D tex)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL 无文件系统,跳过
#else
            try
            {
                Directory.CreateDirectory(Path.Combine(Application.streamingAssetsPath, CacheFolder));
                File.WriteAllBytes(CachePath(name), ImageConversion.EncodeToPNG(tex));
            }
            catch { /* 写缓存失败不影响运行 */ }
#endif
        }

        private static Sprite MakeSprite(Texture2D tex)
        {
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================ 绘制:玩家 ============================

        private static void DrawPlayer(PixelCanvas c)
        {
            // 机头朝上的战斗机,画布 128,中心 (64,64)
            var hull = new Color(0.62f, 0.78f, 0.95f);
            var hullDark = new Color(0.34f, 0.48f, 0.72f);
            var accent = new Color(0.25f, 0.62f, 0.95f);

            // 后掠主翼
            c.Triangle(52, 74, 8, 26, 56, 44, hullDark);
            c.Triangle(76, 74, 120, 26, 72, 44, hullDark);
            // 尾翼
            c.Triangle(54, 30, 34, 8, 58, 14, hullDark);
            c.Triangle(74, 30, 94, 8, 70, 14, hullDark);
            // 机身(修长六边形)
            c.Triangle(64, 118, 48, 66, 80, 66, hull);
            c.Triangle(48, 66, 80, 66, 74, 22, hull);
            c.Triangle(48, 66, 74, 22, 54, 22, hull);
            // 机身中缝阴影
            c.Triangle(64, 112, 60, 30, 68, 30, new Color(0.5f, 0.66f, 0.86f));
            // 机头高光
            c.Triangle(64, 118, 58, 92, 70, 92, new Color(0.85f, 0.94f, 1f));
            // 座舱盖
            c.Ellipse(64, 82, 7, 13, new Color(0.35f, 0.95f, 1f));
            c.Ellipse(64, 84, 4, 8, new Color(0.75f, 1f, 1f));
            // 翼尖航灯
            c.AdditiveCircle(12, 28, 4, new Color(1f, 0.35f, 0.3f));
            c.AdditiveCircle(116, 28, 4, new Color(0.3f, 1f, 0.45f));
            // 发动机尾焰(加法辉光)
            c.AdditiveEllipse(56, 18, 5, 9, new Color(1f, 0.75f, 0.35f));
            c.AdditiveEllipse(72, 18, 5, 9, new Color(1f, 0.75f, 0.35f));
            c.AdditiveEllipse(56, 20, 2.5f, 5, new Color(1f, 1f, 0.85f));
            c.AdditiveEllipse(72, 20, 2.5f, 5, new Color(1f, 1f, 0.85f));
            // 机翼武器挂架装饰
            c.Rect(44, 56, 48, 70, accent);
            c.Rect(80, 56, 84, 70, accent);
        }

        // ============================ 绘制:敌机 ============================

        private static void DrawEnemySmall(PixelCanvas c)
        {
            // 朝下的小型机,画布 96
            var body = new Color(0.91f, 0.42f, 0.38f);
            var dark = new Color(0.62f, 0.25f, 0.28f);
            c.Triangle(48, 8, 24, 72, 72, 72, body);          // 主机(尖头朝下)
            c.Triangle(24, 72, 72, 72, 60, 84, dark);          // 上缘
            c.Triangle(24, 72, 60, 84, 36, 84, dark);
            c.Triangle(48, 30, 14, 58, 40, 62, dark);          // 左短翼
            c.Triangle(48, 30, 82, 58, 56, 62, dark);          // 右短翼
            c.Ellipse(48, 56, 6, 8, new Color(0.25f, 0.1f, 0.12f));   // 座舱
            c.AdditiveCircle(48, 82, 5, new Color(0.45f, 0.8f, 1f));  // 尾焰(朝上)
        }

        private static void DrawEnemyMedium(PixelCanvas c)
        {
            // 朝下的中型机,画布 128
            var hull = new Color(0.85f, 0.68f, 0.38f);
            var dark = new Color(0.58f, 0.44f, 0.22f);
            c.Rect(52, 18, 76, 112, hull);                            // 机身
            c.Triangle(64, 10, 52, 30, 76, 30, hull);                 // 机头(朝下)
            c.Triangle(52, 66, 14, 96, 52, 92, dark);                 // 左翼
            c.Triangle(76, 66, 114, 96, 76, 92, dark);                // 右翼
            c.Rect(46, 96, 82, 112, dark);                            // 尾段
            c.Circle(30, 88, 9, dark);                                // 左发动机
            c.Circle(98, 88, 9, dark);                                // 右发动机
            c.Ellipse(64, 74, 8, 11, new Color(0.2f, 0.12f, 0.08f));  // 座舱
            c.AdditiveCircle(30, 96, 4.5f, new Color(1f, 0.6f, 0.3f));
            c.AdditiveCircle(98, 96, 4.5f, new Color(1f, 0.6f, 0.3f));
            c.Rect(60, 22, 68, 44, new Color(1f, 0.85f, 0.5f));       // 机头炮
        }

        private static void DrawEnemyLarge(PixelCanvas c)
        {
            // 朝下的大型机,画布 192
            var hull = new Color(0.55f, 0.62f, 0.72f);
            var dark = new Color(0.36f, 0.42f, 0.52f);
            c.Triangle(96, 14, 58, 70, 134, 70, hull);                // 舰艏
            c.Rect(58, 70, 134, 158, hull);                           // 主体
            c.Triangle(58, 100, 16, 140, 58, 132, dark);              // 左舷
            c.Triangle(134, 100, 176, 140, 134, 132, dark);           // 右舷
            c.Rect(70, 150, 122, 176, dark);                          // 舰尾
            c.Circle(96, 108, 17, new Color(0.25f, 0.28f, 0.34f));    // 核心舱
            c.AdditiveCircle(96, 108, 10, new Color(1f, 0.4f, 0.35f));
            c.Circle(66, 150, 8, dark);
            c.Circle(126, 150, 8, dark);
            c.AdditiveCircle(66, 158, 4, new Color(0.5f, 0.8f, 1f));
            c.AdditiveCircle(126, 158, 4, new Color(0.5f, 0.8f, 1f));
            c.Rect(88, 30, 104, 66, new Color(0.7f, 0.78f, 0.9f));    // 舰艏装甲
        }

        private static void DrawEnemyBoss(PixelCanvas c)
        {
            // 朝下的 BOSS 战舰,画布 256
            var hull = new Color(0.45f, 0.3f, 0.5f);
            var hull2 = new Color(0.62f, 0.42f, 0.6f);
            var dark = new Color(0.28f, 0.2f, 0.34f);
            c.Triangle(128, 16, 84, 96, 172, 96, hull2);              // 舰艏冲角
            c.Rect(84, 96, 172, 216, hull2);                          // 中央舰体
            c.Triangle(84, 120, 18, 180, 84, 168, hull);              // 左巨翼
            c.Triangle(172, 120, 238, 180, 172, 168, hull);           // 右巨翼
            c.Triangle(84, 150, 40, 208, 84, 196, dark);              // 左副翼
            c.Triangle(172, 150, 216, 208, 172, 196, dark);           // 右副翼
            c.Rect(96, 204, 160, 232, dark);                          // 舰桥
            c.Circle(128, 150, 26, new Color(0.16f, 0.1f, 0.2f));     // 主炮核心
            c.AdditiveCircle(128, 150, 17, new Color(1f, 0.3f, 0.5f));
            c.AdditiveCircle(128, 150, 8, new Color(1f, 0.85f, 0.9f));
            c.Circle(52, 176, 11, dark);
            c.Circle(204, 176, 11, dark);
            c.AdditiveCircle(52, 186, 5, new Color(0.5f, 0.8f, 1f));
            c.AdditiveCircle(204, 186, 5, new Color(0.5f, 0.8f, 1f));
            c.Rect(120, 40, 136, 96, new Color(0.75f, 0.6f, 0.8f));   // 冲角装甲
            // 舷侧炮位
            c.Rect(70, 128, 82, 148, dark);
            c.Rect(174, 128, 186, 148, dark);
        }

        // ============================ 绘制:子弹 ============================

        private static void DrawPlayerBullet(PixelCanvas c)
        {
            // 竖直能量弹,画布 64(细长)
            c.AdditiveEllipse(32, 32, 12, 26, new Color(0.15f, 0.45f, 1f) * 0.55f); // 外晕
            c.AdditiveEllipse(32, 32, 5.5f, 20, new Color(0.45f, 0.8f, 1f));
            c.AdditiveEllipse(32, 34, 2.5f, 14, new Color(0.95f, 1f, 1f));
        }

        private static void DrawEnemyBullet(PixelCanvas c)
        {
            // 球形敌弹,画布 64
            c.AdditiveCircle(32, 32, 24, new Color(1f, 0.25f, 0.2f) * 0.4f);
            c.AdditiveCircle(32, 32, 14, new Color(1f, 0.45f, 0.25f));
            c.AdditiveCircle(32, 32, 7, new Color(1f, 0.95f, 0.75f));
        }

        // ============================ 绘制:道具 ============================

        private static void DrawPowerupBase(PixelCanvas c, Color border)
        {
            c.RoundedRect(8, 8, 88, 88, 18, new Color(0.09f, 0.12f, 0.2f));
            c.RoundedRing(8, 8, 88, 88, 18, 4, border);
        }

        private static void DrawPowerDouble(PixelCanvas c)
        {
            DrawPowerupBase(c, new Color(0.3f, 0.75f, 1f));
            // 双弹图标
            c.AdditiveEllipse(34, 50, 5, 18, new Color(0.5f, 0.85f, 1f));
            c.AdditiveEllipse(62, 50, 5, 18, new Color(0.5f, 0.85f, 1f));
            c.AdditiveEllipse(34, 52, 2.2f, 12, new Color(1f, 1f, 1f));
            c.AdditiveEllipse(62, 52, 2.2f, 12, new Color(1f, 1f, 1f));
        }

        private static void DrawPowerBomb(PixelCanvas c)
        {
            DrawPowerupBase(c, new Color(1f, 0.7f, 0.25f));
            c.Circle(44, 42, 22, new Color(0.12f, 0.12f, 0.16f));          // 弹体
            c.Circle(44, 42, 22, new Color(0.25f, 0.25f, 0.3f) * 0.25f);
            c.AdditiveCircle(37, 50, 6, new Color(0.55f, 0.55f, 0.6f));     // 高光
            c.Rect(56, 58, 66, 66, new Color(0.45f, 0.35f, 0.25f));         // 引信座
            c.AdditiveCircle(66, 70, 7, new Color(1f, 0.8f, 0.3f));         // 火花
            c.AdditiveCircle(66, 70, 3, new Color(1f, 1f, 0.9f));
        }

        private static void DrawPowerLife(PixelCanvas c)
        {
            DrawPowerupBase(c, new Color(0.35f, 1f, 0.5f));
            c.RoundedRect(40, 22, 56, 74, 5, new Color(0.95f, 0.98f, 1f)); // 十字-竖
            c.RoundedRect(22, 40, 74, 56, 5, new Color(0.95f, 0.98f, 1f)); // 十字-横
        }

        // ============================ 绘制:背景 ============================

        private static void DrawBackground(PixelCanvas c)
        {
            int w = c.W, h = c.H;
            // 纵向深空渐变(可平铺:上下端同色)
            var top = new Color(0.035f, 0.05f, 0.11f);
            var mid = new Color(0.055f, 0.075f, 0.17f);
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                // 对称渐变 → 垂直平铺无缝
                float k = 1f - Math.Abs(t - 0.5f) * 2f;
                var col = Color.Lerp(top, mid, k);
                c.FillRow(y, col);
            }
            var rng = new System.Random(20260927);
            // 星云(低透明加法光斑)
            for (int i = 0; i < 7; i++)
            {
                float x = (float)rng.NextDouble() * w;
                float y = (float)rng.NextDouble() * h;
                float r = 40 + (float)rng.NextDouble() * 90;
                var tint = rng.NextDouble() < 0.5
                    ? new Color(0.25f, 0.2f, 0.55f)
                    : new Color(0.15f, 0.35f, 0.55f);
                c.AdditiveCircleTiled(x, y, r, tint * 0.10f);
            }
            // 星点
            for (int i = 0; i < 160; i++)
            {
                float x = (float)rng.NextDouble() * w;
                float y = (float)rng.NextDouble() * h;
                float bright = 0.35f + (float)rng.NextDouble() * 0.65f;
                float r = rng.NextDouble() < 0.85 ? 0.9f : 1.8f;
                c.AdditiveCircleTiled(x, y, r, new Color(bright, bright, bright * 0.95f, bright));
            }
        }

        private static void DrawWhite(PixelCanvas c)
        {
            c.Rect(0, 0, c.W, c.H, Color.white);
        }
    }

    // -----------------------------------------------------------------------
    // 浮点抗锯齿画布
    // -----------------------------------------------------------------------
    internal sealed class PixelCanvas
    {
        public readonly int W, H;
        private readonly float[] _r, _g, _b, _a;

        public PixelCanvas(int w, int h)
        {
            W = w; H = h;
            int n = w * h;
            _r = new float[n]; _g = new float[n]; _b = new float[n]; _a = new float[n];
        }

        public void FillRow(int y, Color col)
        {
            int baseIdx = y * W;
            for (int x = 0; x < W; x++)
            {
                int i = baseIdx + x;
                _r[i] = col.r; _g[i] = col.g; _b[i] = col.b; _a[i] = Mathf.Max(_a[i], col.a);
            }
        }

        private void Blend(int i, Color col, float cov)
        {
            float alpha = cov * col.a;
            if (alpha <= 0f) return;
            float inv = 1f - alpha;
            _r[i] = col.r * alpha + _r[i] * inv;
            _g[i] = col.g * alpha + _g[i] * inv;
            _b[i] = col.b * alpha + _b[i] * inv;
            _a[i] = alpha + _a[i] * inv;
        }

        private void BlendAdd(int i, Color col, float cov)
        {
            float k = cov * col.a;
            if (k <= 0f) return;
            _r[i] += col.r * k;
            _g[i] += col.g * k;
            _b[i] += col.b * k;
            _a[i] = Mathf.Max(_a[i], Mathf.Min(1f, k));
        }

        private delegate float ShapeDist(float px, float py);

        private void ForEachTexel(ShapeDist dist, Color col, bool additive, float pad = 1f)
        {
            // 遍历形状包围盒
            for (int y = 0; y < H; y++)
            {
                int rowBase = y * W;
                for (int x = 0; x < W; x++)
                {
                    float d = dist(x + 0.5f, y + 0.5f);
                    float cov = Mathf.Clamp(0.5f - d, 0f, 1f);
                    if (cov <= 0f) continue;
                    int i = rowBase + x;
                    if (additive) BlendAdd(i, col, cov);
                    else Blend(i, col, cov);
                }
            }
        }

        public void Circle(float cx, float cy, float r, Color col)
        {
            ForEachTexel((px, py) =>
            {
                float dx = px - cx, dy = py - cy;
                return Mathf.Sqrt(dx * dx + dy * dy) - r;
            }, col, false);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color col)
        {
            ForEachTexel((px, py) =>
            {
                float nx = (px - cx) / rx, ny = (py - cy) / ry;
                return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
            }, col, false);
        }

        public void Triangle(float x0, float y0, float x1, float y1, float x2, float y2, Color col)
        {
            ForEachTexel((px, py) => TriangleSdf(px, py, x0, y0, x1, y1, x2, y2), col, false);
        }

        public void Rect(float x0, float y0, float x1, float y1, Color col)
        {
            ForEachTexel((px, py) =>
            {
                float dx = Mathf.Max(x0 - px, px - x1);
                float dy = Mathf.Max(y0 - py, py - y1);
                return Mathf.Max(dx, dy);
            }, col, false);
        }

        public void RoundedRect(float x0, float y0, float x1, float y1, float radius, Color col)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float hw = (x1 - x0) * 0.5f - radius, hh = (y1 - y0) * 0.5f - radius;
            ForEachTexel((px, py) =>
            {
                float qx = Mathf.Abs(px - cx) - hw;
                float qy = Mathf.Abs(py - cy) - hh;
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            }, col, false);
        }

        /// <summary>圆角矩形描边。</summary>
        public void RoundedRing(float x0, float y0, float x1, float y1, float radius, float width, Color col)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float hw = (x1 - x0) * 0.5f - radius, hh = (y1 - y0) * 0.5f - radius;
            ForEachTexel((px, py) =>
            {
                float qx = Mathf.Abs(px - cx) - hw;
                float qy = Mathf.Abs(py - cy) - hh;
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                return Mathf.Abs(d) - width * 0.5f;
            }, col, false);
        }

        public void AdditiveCircle(float cx, float cy, float r, Color col)
        {
            ForEachTexel((px, py) =>
            {
                float dx = px - cx, dy = py - cy;
                return Mathf.Sqrt(dx * dx + dy * dy) - r;
            }, col, true);
        }

        public void AdditiveEllipse(float cx, float cy, float rx, float ry, Color col)
        {
            ForEachTexel((px, py) =>
            {
                float nx = (px - cx) / rx, ny = (py - cy) / ry;
                return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
            }, col, true);
        }

        /// <summary>水平/垂直环绕平铺的加法光斑(背景用)。</summary>
        public void AdditiveCircleTiled(float cx, float cy, float r, Color col)
        {
            ForEachTexel((px, py) =>
            {
                float best = float.MaxValue;
                for (int ox = -1; ox <= 1; ox++)
                {
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        float dx = px - (cx + ox * W);
                        float dy = py - (cy + oy * H);
                        float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                        if (d < best) best = d;
                    }
                }
                return best;
            }, col, true);
        }

        private static float TriangleSdf(float px, float py,
                                         float x0, float y0, float x1, float y1, float x2, float y2)
        {
            float d0 = EdgeDist(px, py, x0, y0, x1, y1);
            float d1 = EdgeDist(px, py, x1, y1, x2, y2);
            float d2 = EdgeDist(px, py, x2, y2, x0, y0);
            float edge = Mathf.Min(d0, Mathf.Min(d1, d2));
            bool inside = PointInTriangle(px, py, x0, y0, x1, y1, x2, y2);
            return inside ? -edge : edge;
        }

        private static float EdgeDist(float px, float py, float x0, float y0, float x1, float y1)
        {
            float ex = x1 - x0, ey = y1 - y0;
            float wx = px - x0, wy = py - y0;
            float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey + 1e-6f));
            float dx = px - (x0 + ex * t), dy = py - (y0 + ey * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static bool PointInTriangle(float px, float py,
                                            float x0, float y0, float x1, float y1, float x2, float y2)
        {
            float s1 = (x1 - x0) * (py - y0) - (y1 - y0) * (px - x0);
            float s2 = (x2 - x1) * (py - y1) - (y2 - y1) * (px - x1);
            float s3 = (x0 - x2) * (py - y2) - (y0 - y2) * (px - x2);
            bool hasNeg = s1 < 0 || s2 < 0 || s3 < 0;
            bool hasPos = s1 > 0 || s2 > 0 || s3 > 0;
            return !(hasNeg && hasPos);
        }

        public Texture2D ToTexture(string name)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = name };
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color(
                    Mathf.Clamp01(_r[i]),
                    Mathf.Clamp01(_g[i]),
                    Mathf.Clamp01(_b[i]),
                    Mathf.Clamp01(_a[i]));
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
