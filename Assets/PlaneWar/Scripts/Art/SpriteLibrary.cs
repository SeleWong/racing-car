using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 所有游戏图片。优先使用 GameConfig 中指定的 Sprite（可替换为原版素材），
    /// 为空时在运行时程序化生成，保证项目零依赖可运行。
    /// </summary>
    public class SpriteLibrary
    {
        public Sprite Player1, Player2;
        public Sprite Bullet, DoubleBullet;
        public Sprite Background;
        public Sprite BulletSupply, BombSupply, BombIcon;
        public readonly Sprite[] Enemy = new Sprite[3];
        public readonly Sprite[] EnemyHit = new Sprite[3];
        public Sprite[] Explosion;

        // UI
        public Sprite White, RoundRect, Circle, PauseIcon, PlayIcon;

        private static readonly Color Outline = C(40, 52, 70);

        public static SpriteLibrary Build(GameConfig cfg)
        {
            var lib = new SpriteLibrary();

            lib.Player1 = cfg.playerSprite != null ? cfg.playerSprite : DrawPlayer(true).ToSprite("hero1", cfg.playerSize.x);
            lib.Player2 = cfg.playerSprite2 != null ? cfg.playerSprite2
                        : cfg.playerSprite != null ? cfg.playerSprite : DrawPlayer(false).ToSprite("hero2", cfg.playerSize.x);

            lib.Bullet = cfg.bulletSprite != null ? cfg.bulletSprite : DrawBullet(false).ToSprite("bullet1", cfg.bulletSize.x);
            lib.DoubleBullet = cfg.doubleBulletSprite != null ? cfg.doubleBulletSprite : DrawBullet(true).ToSprite("bullet2", cfg.bulletSize.x);

            lib.BulletSupply = cfg.bulletSupplySprite != null ? cfg.bulletSupplySprite : DrawSupply(false).ToSprite("bullet_supply", cfg.supplySize.x);
            lib.BombSupply = cfg.bombSupplySprite != null ? cfg.bombSupplySprite : DrawSupply(true).ToSprite("bomb_supply", cfg.supplySize.x);
            lib.BombIcon = cfg.bombIconSprite != null ? cfg.bombIconSprite : DrawBombIcon().ToSprite("bomb", 1f);

            lib.Background = cfg.backgroundSprite != null ? cfg.backgroundSprite : DrawBackground();

            for (int i = 0; i < 3; i++)
            {
                var ec = cfg.GetEnemy((EnemyKind)i);
                PixelCanvas canvas = null;
                if (ec.sprite == null)
                {
                    canvas = DrawEnemy((EnemyKind)i);
                    lib.Enemy[i] = canvas.TintedCopy(Color.white, 0f).ToSprite("enemy" + (i + 1), ec.size.x);
                }
                else lib.Enemy[i] = ec.sprite;

                if (ec.hitSprite != null) lib.EnemyHit[i] = ec.hitSprite;
                else if (canvas != null) lib.EnemyHit[i] = canvas.TintedCopy(Color.white, 0.55f).ToSprite("enemy" + (i + 1) + "_hit", ec.size.x);
                else lib.EnemyHit[i] = lib.Enemy[i];
            }

            lib.Explosion = DrawExplosionFrames(6);

            // UI 资源
            var white = new PixelCanvas(4, 4);
            white.Rect(0, 0, 1, 1, Color.white);
            lib.White = white.ToSprite("ui_white", 1f);

            var rr = new PixelCanvas(64, 64);
            rr.RoundRect(0.0f, 0.0f, 1f, 1f, 0.35f, Color.white);
            lib.RoundRect = rr.ToSprite("ui_roundrect", 1f, new Vector4(24, 24, 24, 24));

            var circle = new PixelCanvas(64, 64);
            circle.Ellipse(0.5f, 0.5f, 0.48f, 0.48f, Color.white);
            lib.Circle = circle.ToSprite("ui_circle", 1f);

            var pause = new PixelCanvas(64, 64);
            pause.RoundRect(0.26f, 0.2f, 0.42f, 0.8f, 0.05f, Color.white);
            pause.RoundRect(0.58f, 0.2f, 0.74f, 0.8f, 0.05f, Color.white);
            lib.PauseIcon = pause.ToSprite("ui_pause", 1f);

            var play = new PixelCanvas(64, 64);
            play.Polygon(new[] { new Vector2(0.3f, 0.18f), new Vector2(0.82f, 0.5f), new Vector2(0.3f, 0.82f) }, Color.white);
            lib.PlayIcon = play.ToSprite("ui_play", 1f);

            return lib;
        }

        public Sprite GetEnemy(EnemyKind k) { return Enemy[(int)k]; }
        public Sprite GetEnemyHit(EnemyKind k) { return EnemyHit[(int)k]; }

        // ------------------------------------------------------------------ 绘制

        private static Color C(int r, int g, int b, float a = 1f) { return new Color(r / 255f, g / 255f, b / 255f, a); }

        /// <summary>带描边的对称多边形：先在 8 个方向偏移画描边色，再画填充色。</summary>
        private static void SymOutlined(PixelCanvas c, Color fill, float w, params float[] xy)
        {
            var pts = PixelCanvas.Mirror(xy);
            var tmp = new Vector2[pts.Length];
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    for (int i = 0; i < pts.Length; i++) tmp[i] = pts[i] + new Vector2(dx * w, dy * w);
                    c.Polygon(tmp, Outline);
                }
            }
            c.Polygon(pts, fill);
        }

        /// <summary>画一个多边形及其左右镜像（不相连），用于机翼条纹等。</summary>
        private static void MirrorPair(PixelCanvas c, Color color, params float[] xy)
        {
            int n = xy.Length / 2;
            var a = new Vector2[n];
            var b = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                a[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
                b[i] = new Vector2(1f - xy[i * 2], xy[i * 2 + 1]);
            }
            c.Polygon(a, color);
            c.Polygon(b, color);
        }

        private static PixelCanvas DrawPlayer(bool longFlame)
        {
            var c = new PixelCanvas(128, 160);
            const float w = 0.012f;
            float fl = longFlame ? 0f : 0.05f;
            // 尾焰（两帧交替）
            c.SymPolygon(C(255, 170, 60, 0.9f), 0.5f, 0.22f, 0.575f, 0.16f, 0.54f, fl + 0.04f, 0.5f, fl);
            c.SymPolygon(C(255, 240, 170), 0.5f, 0.2f, 0.54f, 0.15f, 0.515f, fl + 0.07f, 0.5f, fl + 0.04f);
            // 主翼
            SymOutlined(c, C(120, 170, 230), w, 0.56f, 0.66f, 0.97f, 0.40f, 0.97f, 0.31f, 0.60f, 0.34f, 0.58f, 0.36f);
            // 尾翼
            SymOutlined(c, C(120, 170, 230), w, 0.55f, 0.30f, 0.75f, 0.18f, 0.75f, 0.13f, 0.52f, 0.16f);
            // 机身
            SymOutlined(c, C(235, 242, 250), w, 0.5f, 0.99f, 0.545f, 0.93f, 0.58f, 0.78f, 0.60f, 0.50f, 0.58f, 0.22f, 0.54f, 0.15f, 0.5f, 0.14f);
            // 机翼条纹
            MirrorPair(c, C(240, 110, 130), 0.66f, 0.52f, 0.92f, 0.395f, 0.92f, 0.355f, 0.66f, 0.47f);
            // 驾驶舱
            c.Ellipse(0.5f, 0.74f, 0.035f, 0.09f, C(60, 110, 180));
            c.Ellipse(0.49f, 0.77f, 0.012f, 0.035f, C(200, 230, 255));
            // 发动机
            MirrorPair(c, C(90, 110, 140), 0.66f, 0.30f, 0.70f, 0.30f, 0.70f, 0.40f, 0.66f, 0.40f);
            return c;
        }

        private static PixelCanvas DrawEnemy(EnemyKind kind)
        {
            PixelCanvas c;
            switch (kind)
            {
                case EnemyKind.Small:
                {
                    c = new PixelCanvas(96, 72);
                    const float w = 0.02f;
                    SymOutlined(c, C(150, 160, 170), w, 0.53f, 0.75f, 0.97f, 0.55f, 0.97f, 0.40f, 0.56f, 0.38f);
                    SymOutlined(c, C(150, 160, 170), w, 0.53f, 0.25f, 0.72f, 0.12f, 0.72f, 0.03f, 0.5f, 0.05f);
                    SymOutlined(c, C(215, 220, 225), w, 0.5f, 0.99f, 0.55f, 0.90f, 0.57f, 0.6f, 0.55f, 0.2f, 0.5f, 0.05f);
                    c.Ellipse(0.5f, 0.72f, 0.03f, 0.12f, C(60, 70, 90));
                    MirrorPair(c, C(120, 190, 120), 0.82f, 0.53f, 0.93f, 0.505f, 0.93f, 0.435f, 0.82f, 0.445f);
                    break;
                }
                case EnemyKind.Medium:
                {
                    c = new PixelCanvas(112, 160);
                    const float w = 0.014f;
                    SymOutlined(c, C(110, 140, 120), w, 0.56f, 0.70f, 0.98f, 0.52f, 0.98f, 0.42f, 0.60f, 0.40f);
                    SymOutlined(c, C(110, 140, 120), w, 0.55f, 0.24f, 0.80f, 0.14f, 0.80f, 0.06f, 0.52f, 0.08f);
                    SymOutlined(c, C(185, 200, 185), w, 0.5f, 0.99f, 0.57f, 0.93f, 0.62f, 0.75f, 0.62f, 0.3f, 0.56f, 0.08f, 0.5f, 0.06f);
                    MirrorPair(c, C(70, 80, 90), 0.72f, 0.36f, 0.78f, 0.36f, 0.78f, 0.50f, 0.72f, 0.50f);
                    c.Ellipse(0.5f, 0.78f, 0.045f, 0.09f, C(60, 70, 90));
                    c.SymPolygon(C(220, 90, 80), 0.5f, 0.62f, 0.56f, 0.58f, 0.56f, 0.5f, 0.5f, 0.46f);
                    break;
                }
                default:
                {
                    c = new PixelCanvas(200, 300);
                    const float w = 0.008f;
                    SymOutlined(c, C(120, 110, 150), w, 0.56f, 0.78f, 0.99f, 0.56f, 0.99f, 0.46f, 0.62f, 0.42f);
                    SymOutlined(c, C(120, 110, 150), w, 0.55f, 0.26f, 0.82f, 0.16f, 0.82f, 0.08f, 0.52f, 0.1f);
                    SymOutlined(c, C(200, 195, 215), w, 0.5f, 0.995f, 0.6f, 0.95f, 0.66f, 0.82f, 0.68f, 0.5f, 0.64f, 0.2f, 0.56f, 0.07f, 0.5f, 0.06f);
                    float[] xs = { 0.74f, 0.86f };
                    for (int i = 0; i < xs.Length; i++)
                    {
                        float x = xs[i];
                        c.RoundRect(x - 0.03f, 0.38f, x + 0.03f, 0.56f, 0.02f, C(70, 70, 90));
                        c.RoundRect(1f - x - 0.03f, 0.38f, 1f - x + 0.03f, 0.56f, 0.02f, C(70, 70, 90));
                    }
                    c.Ellipse(0.5f, 0.84f, 0.07f, 0.07f, C(60, 70, 110));
                    c.Ellipse(0.48f, 0.86f, 0.025f, 0.025f, C(200, 220, 255));
                    c.SymPolygon(C(235, 100, 120), 0.5f, 0.66f, 0.6f, 0.62f, 0.6f, 0.52f, 0.5f, 0.48f);
                    c.SymPolygon(C(255, 210, 90), 0.5f, 0.40f, 0.58f, 0.36f, 0.58f, 0.30f, 0.5f, 0.27f);
                    break;
                }
            }
            c.FlipVertical(); // 敌机机头朝下
            return c;
        }

        private static PixelCanvas DrawBullet(bool isDouble)
        {
            var c = new PixelCanvas(16, 40);
            Color col = isDouble ? C(80, 160, 255) : C(255, 120, 60);
            c.RoundRect(0.1f, 0f, 0.9f, 1f, 0.4f, col);
            c.RoundRect(0.3f, 0.15f, 0.7f, 0.9f, 0.2f, C(255, 250, 220));
            return c;
        }

        private static PixelCanvas DrawSupply(bool bomb)
        {
            var c = new PixelCanvas(88, 128);
            Color col = bomb ? C(230, 80, 80) : C(70, 140, 230);
            // 降落伞
            PixelCanvas.Inside canopy = (x, y) =>
            {
                float dx = (x - 0.5f) / 0.46f, dy = (y - 0.72f) / 0.26f;
                return y >= 0.66f && dx * dx + dy * dy <= 1f;
            };
            c.Fill(canopy, Color.white, 0f, 0.6f, 1f, 1f);
            float[] stripes = { 0.2f, 0.5f, 0.8f };
            for (int i = 0; i < stripes.Length; i++)
            {
                float sx = stripes[i];
                c.Fill((x, y) =>
                {
                    float dx = (x - sx) / 0.08f, dy = (y - 0.72f) / 0.26f;
                    return canopy(x, y) && dx * dx + dy * dy <= 1f;
                }, col, 0f, 0.6f, 1f, 1f);
            }
            // 伞绳
            float[] ropes = { 0.06f, 0.36f, 0.64f, 0.94f };
            for (int i = 0; i < ropes.Length; i++)
            {
                float x0 = ropes[i];
                c.Polygon(new[] { new Vector2(x0, 0.66f), new Vector2(x0 + 0.02f, 0.66f), new Vector2(0.51f, 0.36f), new Vector2(0.49f, 0.36f) }, C(120, 120, 120));
            }
            // 箱子
            c.RoundRect(0.22f, 0.02f, 0.78f, 0.40f, 0.08f, Outline);
            c.RoundRect(0.25f, 0.04f, 0.75f, 0.38f, 0.06f, col);
            if (bomb)
            {
                c.Ellipse(0.5f, 0.19f, 0.13f, 0.10f, C(40, 40, 50));
                c.Rect(0.47f, 0.28f, 0.53f, 0.33f, C(40, 40, 50));
            }
            else
            {
                c.RoundRect(0.36f, 0.08f, 0.44f, 0.32f, 0.03f, C(255, 230, 120));
                c.RoundRect(0.56f, 0.08f, 0.64f, 0.32f, 0.03f, C(255, 230, 120));
            }
            return c;
        }

        private static PixelCanvas DrawBombIcon()
        {
            var c = new PixelCanvas(64, 64);
            c.Ellipse(0.5f, 0.42f, 0.36f, 0.36f, Outline);
            c.Ellipse(0.5f, 0.42f, 0.31f, 0.31f, C(60, 64, 80));
            c.Ellipse(0.4f, 0.52f, 0.08f, 0.08f, C(150, 155, 175));
            c.RoundRect(0.42f, 0.72f, 0.58f, 0.84f, 0.03f, Outline);
            c.Polygon(new[] { new Vector2(0.52f, 0.82f), new Vector2(0.62f, 0.95f), new Vector2(0.66f, 0.92f), new Vector2(0.56f, 0.8f) }, C(160, 120, 80));
            c.Ellipse(0.66f, 0.94f, 0.06f, 0.05f, C(255, 180, 60));
            return c;
        }

        private static Sprite[] DrawExplosionFrames(int count)
        {
            var frames = new Sprite[count];
            var rnd = new System.Random(1234);
            // 固定的火球碎片方向，保证各帧连贯
            int blobs = 9;
            var dirs = new Vector2[blobs];
            var sizes = new float[blobs];
            for (int i = 0; i < blobs; i++)
            {
                float ang = (float)(i * Mathf.PI * 2 / blobs + rnd.NextDouble() * 0.5);
                dirs[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                sizes[i] = 0.07f + (float)rnd.NextDouble() * 0.06f;
            }
            for (int f = 0; f < count; f++)
            {
                float t = (f + 1f) / count;                 // 0..1
                float alpha = Mathf.Clamp01(1.25f - t);
                var c = new PixelCanvas(96, 96);
                float r = Mathf.Lerp(0.12f, 0.34f, t);
                c.Ellipse(0.5f, 0.5f, r, r, C(255, 140, 50, 0.85f * alpha));
                for (int i = 0; i < blobs; i++)
                {
                    Vector2 p = new Vector2(0.5f, 0.5f) + dirs[i] * Mathf.Lerp(0.1f, 0.38f, t);
                    float s = sizes[i] * (1.2f - t * 0.6f);
                    c.Ellipse(p.x, p.y, s, s, C(255, 110, 40, alpha));
                    c.Ellipse(p.x, p.y, s * 0.5f, s * 0.5f, C(255, 220, 120, alpha));
                }
                float core = Mathf.Lerp(0.16f, 0.02f, t);
                c.Ellipse(0.5f, 0.5f, core, core, C(255, 245, 200, alpha));
                if (t > 0.5f) // 烟
                {
                    c.Ellipse(0.44f, 0.56f, r * 0.5f, r * 0.45f, C(90, 90, 95, 0.5f * alpha));
                    c.Ellipse(0.58f, 0.45f, r * 0.4f, r * 0.4f, C(90, 90, 95, 0.45f * alpha));
                }
                frames[f] = c.ToSprite("explosion_" + f, 1f);
            }
            return frames;
        }

        private static Sprite DrawBackground()
        {
            // 原版风格：浅灰蓝底 + 淡淡的格纹，可无缝平铺
            const int size = 256;
            var c = new PixelCanvas(size, size);
            c.Rect(0f, 0f, 1f, 1f, C(196, 202, 206));
            Color line = C(180, 187, 192);
            for (int i = 0; i < 8; i++)
            {
                float p = i / 8f;
                c.Rect(p, 0f, p + 1.5f / size, 1f, line);
                c.Rect(0f, p, 1f, p + 1.5f / size, line);
            }
            c.Ellipse(0.3f, 0.7f, 0.16f, 0.06f, C(215, 220, 224, 0.8f));
            c.Ellipse(0.38f, 0.73f, 0.1f, 0.06f, C(215, 220, 224, 0.8f));
            c.Ellipse(0.75f, 0.25f, 0.14f, 0.05f, C(215, 220, 224, 0.8f));
            var tex = c.ToTexture("background", FilterMode.Bilinear, TextureWrapMode.Repeat);
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 4.5f, 0, SpriteMeshType.FullRect);
            sp.name = "background";
            return sp;
        }
    }
}
