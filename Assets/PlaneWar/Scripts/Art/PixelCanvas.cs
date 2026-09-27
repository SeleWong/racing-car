using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 极简 CPU 光栅器：在归一化坐标 (0..1, y 向上) 中画多边形 / 椭圆 / 矩形，2x2 超采样抗锯齿。
    /// 用于在运行时生成全部美术资源，使项目无需导入任何图片即可运行。
    /// </summary>
    public class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color[] Pixels;

        private static readonly Vector2[] SubSamples =
        {
            new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.25f),
            new Vector2(0.25f, 0.75f), new Vector2(0.75f, 0.75f),
        };

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color[width * height];
            for (int i = 0; i < Pixels.Length; i++) Pixels[i] = new Color(0, 0, 0, 0);
        }

        public delegate bool Inside(float x, float y);

        /// <summary>对归一化包围盒内每个像素做超采样覆盖测试并混合颜色。</summary>
        public void Fill(Inside inside, Color color, float minX = 0f, float minY = 0f, float maxX = 1f, float maxY = 1f)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(minX * Width) - 1, 0, Width - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX * Width) + 1, 0, Width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(minY * Height) - 1, 0, Height - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY * Height) + 1, 0, Height - 1);

            for (int py = y0; py <= y1; py++)
            {
                for (int px = x0; px <= x1; px++)
                {
                    int hits = 0;
                    for (int s = 0; s < 4; s++)
                    {
                        float nx = (px + SubSamples[s].x) / Width;
                        float ny = (py + SubSamples[s].y) / Height;
                        if (inside(nx, ny)) hits++;
                    }
                    if (hits == 0) continue;
                    Blend(px, py, color, hits * 0.25f);
                }
            }
        }

        public void Blend(int px, int py, Color c, float coverage)
        {
            int idx = py * Width + px;
            Color dst = Pixels[idx];
            float a = c.a * coverage;
            float outA = a + dst.a * (1f - a);
            if (outA <= 0.0001f) return;
            Color o = (c * a + dst * dst.a * (1f - a)) / outA;
            o.a = outA;
            Pixels[idx] = o;
        }

        public void Polygon(Vector2[] pts, Color color)
        {
            float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                minX = Mathf.Min(minX, pts[i].x); maxX = Mathf.Max(maxX, pts[i].x);
                minY = Mathf.Min(minY, pts[i].y); maxY = Mathf.Max(maxY, pts[i].y);
            }
            Fill((x, y) => PointInPolygon(pts, x, y), color, minX, minY, maxX, maxY);
        }

        /// <summary>对称多边形：只给出右半边轮廓点（从上到下、x ≥ 0.5），自动镜像左半边。</summary>
        public void SymPolygon(Color color, params float[] rightSideXY)
        {
            Polygon(Mirror(rightSideXY), color);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color color)
        {
            Fill((x, y) =>
            {
                float dx = (x - cx) / rx, dy = (y - cy) / ry;
                return dx * dx + dy * dy <= 1f;
            }, color, cx - rx, cy - ry, cx + rx, cy + ry);
        }

        public void Rect(float x0, float y0, float x1, float y1, Color color)
        {
            Fill((x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1, color, x0, y0, x1, y1);
        }

        public void RoundRect(float x0, float y0, float x1, float y1, float r, Color color)
        {
            Fill((x, y) =>
            {
                if (x < x0 || x > x1 || y < y0 || y > y1) return false;
                float cx = Mathf.Clamp(x, x0 + r, x1 - r);
                float cy = Mathf.Clamp(y, y0 + r, y1 - r);
                float dx = x - cx, dy = y - cy;
                return dx * dx + dy * dy <= r * r;
            }, color, x0, y0, x1, y1);
        }

        public void FlipVertical()
        {
            for (int y = 0; y < Height / 2; y++)
            {
                int a = y * Width, b = (Height - 1 - y) * Width;
                for (int x = 0; x < Width; x++)
                {
                    Color t = Pixels[a + x];
                    Pixels[a + x] = Pixels[b + x];
                    Pixels[b + x] = t;
                }
            }
        }

        /// <summary>整体向白色插值（受击闪白帧）。</summary>
        public PixelCanvas TintedCopy(Color tint, float amount)
        {
            var c = new PixelCanvas(Width, Height);
            for (int i = 0; i < Pixels.Length; i++)
            {
                Color p = Pixels[i];
                Color o = Color.Lerp(p, tint, amount);
                o.a = p.a;
                c.Pixels[i] = o;
            }
            return c;
        }

        public Texture2D ToTexture(string name, FilterMode filter = FilterMode.Bilinear, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.name = name;
            tex.filterMode = filter;
            tex.wrapMode = wrap;
            tex.SetPixels(Pixels);
            tex.Apply(false, true); // 上传后释放 CPU 内存
            return tex;
        }

        /// <summary>生成 Sprite，并使其世界尺寸正好等于 worldWidth（等比）。</summary>
        public Sprite ToSprite(string name, float worldWidth, Vector4 border = default(Vector4))
        {
            var tex = ToTexture(name);
            float ppu = Width / Mathf.Max(0.0001f, worldWidth);
            var sp = Sprite.Create(tex, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), ppu, 0,
                SpriteMeshType.FullRect, border);
            sp.name = name;
            return sp;
        }

        public static Vector2[] Mirror(float[] rightXY)
        {
            int n = rightXY.Length / 2;
            var pts = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
                pts[i] = new Vector2(rightXY[i * 2], rightXY[i * 2 + 1]);
            for (int i = 0; i < n; i++)
            {
                var p = pts[n - 1 - i];
                pts[n + i] = new Vector2(1f - p.x, p.y);
            }
            return pts;
        }

        public static bool PointInPolygon(Vector2[] poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
