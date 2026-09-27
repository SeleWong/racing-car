using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 轻量 AABB 碰撞工具。飞机大战里所有物体都是轴对齐的，
    /// 自己做碰撞比 Physics2D 更可控、更省（尤其是小游戏 / WebGL）。
    /// </summary>
    public static class HitBox
    {
        public static Rect FromCenter(Vector2 center, Vector2 size)
        {
            return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
        }

        public static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
        }
    }
}
