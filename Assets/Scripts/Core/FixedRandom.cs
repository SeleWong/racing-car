// ---------------------------------------------------------------------------
// 确定性随机数(xorshift128)。
// 客户端本地局与服务器权威局使用相同种子即可复现同一局游戏的随机流,
// 也是回放 / 断线重连后对齐世界状态的基础。纯 C#,无 Unity 依赖。
// ---------------------------------------------------------------------------

namespace PlaneWar.Core
{
    public sealed class FixedRandom
    {
        private uint _x, _y, _z, _w;

        public FixedRandom(uint seed)
        {
            // 用 SplitMix 把 32 位种子扩散到 4 个状态字
            _x = Mix(seed + 0x9E3779B9u);
            _y = Mix(seed + 0x85EBCA6Bu);
            _z = Mix(seed + 0xC2B2AE35u);
            _w = Mix(seed + 0x27D4EB2Fu);
        }

        private static uint Mix(uint v)
        {
            v ^= v >> 16; v *= 0x7FEB352Du;
            v ^= v >> 15; v *= 0x846CA68Bu;
            v ^= v >> 16;
            return v == 0 ? 0x1234567u : v;
        }

        /// <summary>下一个 32 位无符号随机数。</summary>
        public uint NextUInt()
        {
            uint t = _x ^ (_x << 11);
            _x = _y; _y = _z; _z = _w;
            _w = (_w ^ (_w >> 19)) ^ (t ^ (t >> 8));
            return _w;
        }

        /// <summary>[0,1) 均匀分布。</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1.0f / 16777216.0f); // 24 位尾数
        }

        /// <summary>[min,max) 均匀分布。</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>以 probability 概率返回 true。</summary>
        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }
    }
}
