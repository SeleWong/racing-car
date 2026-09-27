using System.Collections.Generic;
using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 游戏世界：管理所有实体的对象池、统一驱动更新、做碰撞检测。
    /// 碰撞规则（与原版一致）：
    ///   子弹 × 敌机  → 敌机扣血，子弹消失；血量归零则爆炸并加分
    ///   敌机 × 玩家  → 玩家坠毁，敌机同时爆炸（不加分）
    ///   补给 × 玩家  → 获得双排子弹 / 炸弹
    /// </summary>
    public class GameWorld : MonoBehaviour
    {
        public readonly List<Bullet> Bullets = new List<Bullet>(64);
        public readonly List<Enemy> Enemies = new List<Enemy>(32);
        public readonly List<Supply> Supplies = new List<Supply>(4);
        public readonly List<Explosion> Explosions = new List<Explosion>(16);

        public Player Player { get; private set; }
        public GameConfig Config { get; private set; }
        public SpriteLibrary Sprites { get; private set; }

        /// <summary>敌机被子弹 / 炸弹击毁（参数：敌机，是否炸弹）。由 GameManager 处理加分。</summary>
        public System.Action<Enemy, bool> OnEnemyDestroyed;
        public System.Action<Supply> OnSupplyCollected;
        public System.Action<Enemy> OnPlayerCollided;

        private ObjectPool<Bullet> _bulletPool;
        private ObjectPool<Enemy> _enemyPool;
        private ObjectPool<Supply> _supplyPool;
        private ObjectPool<Explosion> _explosionPool;

        public void Init(GameConfig cfg, SpriteLibrary sprites)
        {
            Config = cfg;
            Sprites = sprites;
            _bulletPool = new ObjectPool<Bullet>(Bullet.Create, CreateRoot("Bullets"), 40);
            _enemyPool = new ObjectPool<Enemy>(Enemy.Create, CreateRoot("Enemies"), 20);
            _supplyPool = new ObjectPool<Supply>(Supply.Create, CreateRoot("Supplies"), 2);
            _explosionPool = new ObjectPool<Explosion>(Explosion.Create, CreateRoot("Explosions"), 10);

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            Player = playerGo.AddComponent<Player>();
            Player.Init(cfg, sprites, this);
        }

        private Transform CreateRoot(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        // ------------------------------------------------------------------ 生成

        public Bullet SpawnBullet(Vector2 pos, bool isDouble)
        {
            var b = _bulletPool.Get();
            b.Setup(isDouble ? Sprites.DoubleBullet : Sprites.Bullet, pos, Config.bulletSpeed, Config.bulletSize, Config.bulletDamage);
            Bullets.Add(b);
            return b;
        }

        public Enemy SpawnEnemy(EnemyKind kind, Vector2 pos, float speed)
        {
            var e = _enemyPool.Get();
            e.Setup(Config.GetEnemy(kind), Sprites.GetEnemy(kind), Sprites.GetEnemyHit(kind), pos, speed);
            Enemies.Add(e);
            GameEvents.RaiseEnemySpawned(kind);
            return e;
        }

        public Supply SpawnSupply(SupplyKind kind, float x)
        {
            var s = _supplyPool.Get();
            s.Setup(kind, kind == SupplyKind.Bomb ? Sprites.BombSupply : Sprites.BulletSupply, x,
                Config.supplySize, Config.supplyEnterSpeed, Config.supplyFallSpeed);
            Supplies.Add(s);
            return s;
        }

        public void SpawnExplosion(Vector2 pos, float size, float duration)
        {
            var e = _explosionPool.Get();
            e.Play(Sprites.Explosion, pos, size, duration);
            Explosions.Add(e);
        }

        public int CountAlive(EnemyKind kind)
        {
            int n = 0;
            for (int i = 0; i < Enemies.Count; i++)
                if (Enemies[i].Kind == kind && !Enemies[i].IsDying) n++;
            return n;
        }

        // ------------------------------------------------------------------ 更新

        /// <summary>移动所有实体并回收离屏 / 动画结束的对象。</summary>
        public void TickEntities(float dt)
        {
            for (int i = Bullets.Count - 1; i >= 0; i--)
                if (!Bullets[i].Tick(dt)) ReleaseBulletAt(i);

            for (int i = Enemies.Count - 1; i >= 0; i--)
                if (!Enemies[i].Tick(dt)) ReleaseEnemyAt(i);

            for (int i = Supplies.Count - 1; i >= 0; i--)
                if (!Supplies[i].Tick(dt)) ReleaseSupplyAt(i);

            TickEffects(dt);
        }

        /// <summary>只更新爆炸特效（玩家坠毁后仍需播放）。</summary>
        public void TickEffects(float dt)
        {
            for (int i = Explosions.Count - 1; i >= 0; i--)
            {
                if (Explosions[i].Tick(dt)) continue;
                _explosionPool.Release(Explosions[i]);
                Explosions.RemoveAt(i);
            }
        }

        public void CheckCollisions()
        {
            // 子弹 × 敌机
            for (int i = Bullets.Count - 1; i >= 0; i--)
            {
                Rect br = Bullets[i].Hitbox;
                for (int j = 0; j < Enemies.Count; j++)
                {
                    Enemy e = Enemies[j];
                    if (e.IsDying || !e.IsOnScreen) continue;
                    if (!HitBox.Overlaps(br, e.Hitbox)) continue;

                    if (e.TakeDamage(Bullets[i].Damage)) HandleEnemyDestroyed(e, false);
                    ReleaseBulletAt(i);
                    break;
                }
            }

            if (Player == null || !Player.IsAlive) return;
            Rect pr = Player.Hitbox;

            // 补给 × 玩家
            for (int i = Supplies.Count - 1; i >= 0; i--)
            {
                if (!HitBox.Overlaps(pr, Supplies[i].Hitbox)) continue;
                var s = Supplies[i];
                if (OnSupplyCollected != null) OnSupplyCollected(s);
                ReleaseSupplyAt(i);
            }

            // 敌机 × 玩家
            if (Player.IsInvincible) return;
            for (int j = 0; j < Enemies.Count; j++)
            {
                Enemy e = Enemies[j];
                if (e.IsDying) continue;
                if (!HitBox.Overlaps(pr, e.Hitbox)) continue;
                e.Kill();
                SpawnExplosion(e.transform.position, e.Config.size.x * 1.3f, e.Config.dieDuration + 0.1f);
                if (OnPlayerCollided != null) OnPlayerCollided(e);
                break;
            }
        }

        /// <summary>炸弹：清除屏幕内所有敌机（计分）。</summary>
        public int DestroyAllOnScreen()
        {
            int count = 0;
            for (int j = 0; j < Enemies.Count; j++)
            {
                Enemy e = Enemies[j];
                if (e.IsDying || !e.IsOnScreen) continue;
                e.Kill();
                HandleEnemyDestroyed(e, true);
                count++;
            }
            return count;
        }

        /// <summary>复活时清屏（不计分）。</summary>
        public void KillAllEnemiesSilently()
        {
            for (int j = 0; j < Enemies.Count; j++)
            {
                Enemy e = Enemies[j];
                if (e.IsDying) continue;
                e.Kill();
                SpawnExplosion(e.transform.position, e.Config.size.x * 1.3f, e.Config.dieDuration + 0.1f);
            }
        }

        private void HandleEnemyDestroyed(Enemy e, bool byBomb)
        {
            SpawnExplosion(e.transform.position, e.Config.size.x * 1.3f, e.Config.dieDuration + 0.1f);
            if (OnEnemyDestroyed != null) OnEnemyDestroyed(e, byBomb);
        }

        public void ClearAll()
        {
            for (int i = Bullets.Count - 1; i >= 0; i--) ReleaseBulletAt(i);
            for (int i = Enemies.Count - 1; i >= 0; i--) ReleaseEnemyAt(i);
            for (int i = Supplies.Count - 1; i >= 0; i--) ReleaseSupplyAt(i);
            for (int i = Explosions.Count - 1; i >= 0; i--) _explosionPool.Release(Explosions[i]);
            Explosions.Clear();
        }

        // 交换删除，O(1)
        private void ReleaseBulletAt(int i) { _bulletPool.Release(Bullets[i]); RemoveSwap(Bullets, i); }
        private void ReleaseEnemyAt(int i) { _enemyPool.Release(Enemies[i]); RemoveSwap(Enemies, i); }
        private void ReleaseSupplyAt(int i) { _supplyPool.Release(Supplies[i]); RemoveSwap(Supplies, i); }

        private static void RemoveSwap<T>(List<T> list, int i)
        {
            int last = list.Count - 1;
            list[i] = list[last];
            list.RemoveAt(last);
        }
    }
}
