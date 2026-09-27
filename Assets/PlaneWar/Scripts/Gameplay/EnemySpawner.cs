using UnityEngine;

namespace PlaneWar
{
    /// <summary>按类型独立计时生成敌机；间隔与速度受当前难度等级影响。</summary>
    public class EnemySpawner
    {
        private readonly GameConfig _cfg;
        private readonly GameWorld _world;
        private readonly float[] _timers = new float[3];

        public EnemySpawner(GameConfig cfg, GameWorld world)
        {
            _cfg = cfg;
            _world = world;
        }

        public void Reset()
        {
            for (int i = 0; i < 3; i++) _timers[i] = _cfg.GetEnemy((EnemyKind)i).firstDelay;
        }

        public void Tick(float dt, DifficultyLevel level)
        {
            for (int i = 0; i < 3; i++)
            {
                var kind = (EnemyKind)i;
                var ec = _cfg.GetEnemy(kind);
                _timers[i] -= dt;
                if (_timers[i] > 0f) continue;

                float interval = ec.spawnInterval * level.spawnIntervalMultiplier;
                // ±20% 随机抖动，避免机械感
                _timers[i] = interval * Random.Range(0.8f, 1.2f);

                if (_world.CountAlive(kind) >= ec.maxAlive) continue;
                Spawn(kind, level);
            }
        }

        public Enemy Spawn(EnemyKind kind, DifficultyLevel level)
        {
            var ec = _cfg.GetEnemy(kind);
            float halfW = ec.size.x * 0.5f;
            float x = Random.Range(WorldBounds.Left + halfW, WorldBounds.Right - halfW);
            float y = WorldBounds.Top + ec.size.y * 0.5f;
            float speed = Random.Range(ec.minSpeed, ec.maxSpeed) * level.speedMultiplier;
            return _world.SpawnEnemy(kind, new Vector2(x, y), speed);
        }
    }

    /// <summary>定时空投补给：双排子弹或炸弹。</summary>
    public class SupplySpawner
    {
        private readonly GameConfig _cfg;
        private readonly GameWorld _world;
        private float _timer;

        public SupplySpawner(GameConfig cfg, GameWorld world)
        {
            _cfg = cfg;
            _world = world;
        }

        public void Reset() { _timer = _cfg.supplyFirstDelay; }

        public void Tick(float dt)
        {
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = _cfg.supplyInterval + Random.Range(-_cfg.supplyIntervalRandom, _cfg.supplyIntervalRandom);
            var kind = Random.value < _cfg.bombSupplyChance ? SupplyKind.Bomb : SupplyKind.DoubleBullet;
            float halfW = _cfg.supplySize.x * 0.5f;
            _world.SpawnSupply(kind, Random.Range(WorldBounds.Left + halfW, WorldBounds.Right - halfW));
        }
    }
}
