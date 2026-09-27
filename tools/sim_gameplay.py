#!/usr/bin/env python3
# ---------------------------------------------------------------------------
# sim_gameplay.py —— GameCore 规则的 Python 镜像仿真(数值自动从
# GameConfig.cs 抽取),用于在无 Unity/ dotnet 的环境下验证整套游戏规则:
#   · 波次按时间表生成,敌机下压;
#   · 玩家自动射击可击杀敌机,计分/击坠正确;
#   · 不移动的玩家会在合理时间被撞死 → GameOver;
#   · 会躲避的玩家可长期生存,分数随时间增长;
#   · 炸弹清屏、道具掉落、难度曲线、BOSS 出现。
# 若本仿真行为异常,说明 GAME_SPEC/GameConfig 数值设计有问题。
# ---------------------------------------------------------------------------
import math
import re
import os
import random
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "Assets/Scripts/Core/GameConfig.cs")


# ------------------------- 从 C# 抽取数值 -------------------------

def load_config():
    src = open(CONFIG, encoding="utf-8").read()
    cfg = {}
    for m in re.finditer(r"public const (float|int|uint) (\w+)\s*=\s*([^;]+);", src):
        expr = m.group(3).strip()
        expr = re.sub(r"(\d)f\b", r"\1", expr)
        # 替换对其他常量的引用
        def sub_ref(mm):
            name = mm.group(0)
            if name in cfg:
                return repr(cfg[name])
            return name
        for _ in range(3):
            expr = re.sub(r"\b[A-Za-z_]\w*\b", sub_ref, expr)
        try:
            cfg[m.group(2)] = eval(expr, {"__builtins__": {}}, {"Math": math, "math": math})
        except Exception as e:
            cfg[m.group(2)] = None
    # 数组表
    def arr(name):
        m = re.search(r"public static readonly (?:float|int)\[\] " + name + r"\s*=\s*\{([^}]*)\}", src)
        return [float(x.strip().rstrip("f")) for x in m.group(1).split(",")]
    cfg["EnemyHp"] = arr("EnemyHp")
    cfg["EnemyScore"] = arr("EnemyScore")
    cfg["EnemyHalfW"] = arr("EnemyHalfW")
    cfg["EnemyHalfH"] = arr("EnemyHalfH")
    cfg["EnemyBaseSpeed"] = arr("EnemyBaseSpeed")
    cfg["EnemySpeedGain"] = arr("EnemySpeedGain")
    cfg["EnemyMaxSpeed"] = arr("EnemyMaxSpeed")
    cfg["EnemyFireIntervalBase"] = arr("EnemyFireIntervalBase")
    cfg["EnemyFireIntervalMin"] = arr("EnemyFireIntervalMin")
    waves = re.search(r"WaveTable\s*=\s*\{(.*?)\};", src, re.S).group(1)
    wt = []
    for row in re.findall(r"\{([^}]*)\}", waves):
        wt.append([float(x.strip().rstrip("f")) for x in row.split(",")])
    cfg["WaveTable"] = wt
    drops = re.search(r"DropChance\s*=\s*\{(.*?)\};", src, re.S).group(1)
    cfg["DropChance"] = [[float(x.strip().rstrip("f")) for x in row.split(",")]
                         for row in re.findall(r"\{([^}]*)\}", drops)]
    return cfg


C = load_config()


# ------------------------- Python 镜像仿真 -------------------------

class Sim:
    def __init__(self, seed=1):
        self.rng = random.Random(seed)
        self.phase = "ready"
        self.time = 0.0
        self.score = 0
        self.kills = 0
        self.lives = C["PlayerStartLives"]
        self.double_left = 0.0
        self.px, self.py = C["WorldWidth"] / 2, 2.4
        self.tx, self.ty, self.has_t = self.px, self.py, False
        self.fire_timer = 0.0
        self.dist_timer = 0.0
        self.wave_timer = 0.0
        self.wave_index = 0
        self.dead_timer = 0.0
        self.alive = True
        self.over_emitted = False
        self.enemies = []      # dict
        self.pbullets = []
        self.ebullets = []
        self.powerups = []
        self.events = []
        self.next_id = 1
        self.level = 1
        self.difficulty = 0.0
        self.max_boss_seen = 0
        self.pickups = {0: 0, 1: 0, 2: 0}
        self.tick = C["Tick"]

    # ---------- 与 GameCore 对应 ----------

    def begin(self):
        self.phase = "playing"
        self.spawn_wave(0)
        self.wave_index = 1
        self.wave_timer = self.delay_after(0)

    def delay_after(self, i):
        nxt = i + 1
        if nxt >= C["WaveCount"]:
            return 3.0
        return C["WaveTable"][nxt][0]

    def spawn_wave(self, i):
        row = C["WaveTable"][i]
        for kind, count in ((0, int(row[1])), (1, int(row[2])), (2, int(row[3])), (3, int(row[4]))):
            for slot in range(count):
                self.spawn_enemy(kind, slot)

    def spawn_enemy(self, kind, slot):
        hw, hh = C["EnemyHalfW"][kind], C["EnemyHalfH"][kind]
        margin = hw + 0.2
        d = self.difficulty
        fi_b, fi_m = C["EnemyFireIntervalBase"][kind], C["EnemyFireIntervalMin"][kind]
        e = {
            "id": self.next_id, "kind": kind,
            "hp": C["EnemyHp"][kind], "x": self.rng.uniform(margin, C["WorldWidth"] - margin),
            "y": C["WorldHeight"] + hh + slot * 1.35,
            "speed": min(C["EnemyBaseSpeed"][kind] + C["EnemySpeedGain"][kind] * d,
                         C["EnemyMaxSpeed"][kind]),
            "fi": 0.0 if fi_b <= 0 else fi_b + (fi_m - fi_b) * d,
            "ft": 0.0, "state": 1 if kind == 3 else 0, "hold": 0.0,
            "sway": self.rng.random() * 6.283,
        }
        self.next_id += 1
        e["ft"] = 0.0 if e["fi"] <= 0 else e["fi"] * self.rng.uniform(0.4, 1.0)
        self.enemies.append(e)

    def set_target(self, x, y):
        self.tx, self.ty, self.has_t = x, y, True

    def step(self):
        if self.phase != "playing":
            self.events.clear()
            return
        dt = self.tick
        self.time += dt
        self.level = int(self.time / C["LevelDuration"]) + 1
        self.difficulty = min(1.0, (self.level - 1) / C["DifficultyMaxLevelSpan"])

        # 波次
        self.wave_timer -= dt
        if self.wave_timer <= 0:
            self.spawn_wave(self.wave_index)
            self.wave_timer = self.delay_after(self.wave_index)
            self.wave_index = (self.wave_index + 1) % C["WaveCount"]

        # 玩家
        if self.alive:
            if self.has_t:
                dx, dy = self.tx - self.px, self.ty - self.py
                dist = math.hypot(dx, dy)
                step = min(dist, C["PlayerMaxMoveSpeed"] * dt)
                if dist > 1e-5:
                    self.px += dx / dist * step
                    self.py += dy / dist * step
            self.px = max(C["PlayerMinX"], min(C["PlayerMaxX"], self.px))
            self.py = max(C["PlayerMinY"], min(C["PlayerMaxY"], self.py))
            self.double_left -= dt
            self.fire_timer -= dt
            while self.fire_timer <= 0:
                my = self.py + C["PlayerHalfH"] + 0.15
                if self.double_left > 0:
                    self.pbullets.append([self.px - C["DoubleShotMuzzleOffsetX"], my])
                    self.pbullets.append([self.px + C["DoubleShotMuzzleOffsetX"], my])
                else:
                    self.pbullets.append([self.px, my])
                self.fire_timer += C["FireInterval"]
            freq = C["DistanceScoreFreqMin"] + (C["DistanceScoreFreqMax"] - C["DistanceScoreFreqMin"]) * self.difficulty
            self.dist_timer += dt * freq
            while self.dist_timer >= 1:
                self.dist_timer -= 1
                self.score += C["ScorePerDistanceTick"]
        else:
            self.dead_timer -= dt
            self.py -= 3.0 * dt
            if self.dead_timer <= 0 and not self.over_emitted:
                self.over_emitted = True
                self.phase = "gameover"

        # 敌机
        for e in self.enemies:
            if e["kind"] == 3:
                self.update_boss(e, dt)
            else:
                e["y"] -= e["speed"] * dt
                if e["kind"] == 0:
                    e["x"] += math.sin(self.time * 1.3 + e["sway"]) * 0.3 * dt
                self.enemy_fire(e, dt)
        self.enemies = [e for e in self.enemies if e["y"] > C["DespawnBelowY"] - C["EnemyHalfH"][e["kind"]]]

        # 子弹
        for b in self.pbullets:
            b[1] += C["PlayerBulletSpeed"] * dt
        self.pbullets = [b for b in self.pbullets if b[1] < C["WorldHeight"] + 1]
        for b in self.ebullets:
            b[0] += b[2] * dt
            b[1] += b[3] * dt
        self.ebullets = [b for b in self.ebullets
                         if b[1] > C["DespawnBelowY"] and -1 < b[0] < C["WorldWidth"] + 1]
        # 道具
        for p in self.powerups:
            p[1] -= C["PowerupSpeed"] * dt
        self.powerups = [p for p in self.powerups if p[1] > -1]

        self.collide()

    def update_boss(self, e, dt):
        if e["state"] == 1:
            e["y"] -= e["speed"] * 2.2 * dt
            if e["y"] <= C["BossHoldY"]:
                e["y"] = C["BossHoldY"]
                e["state"] = 2
        elif e["state"] == 2:
            e["hold"] += dt
            track = self.px - e["x"]
            e["x"] += max(-0.72, min(0.72, track * 1.2)) * dt
            self.enemy_fire(e, dt)
            if e["hold"] >= C["BossHoldTime"]:
                e["state"] = 3
        else:
            e["y"] -= e["speed"] * 2.6 * dt
            e["x"] += (0.5 if self.px > e["x"] else -0.5) * dt

    def enemy_fire(self, e, dt):
        if e["fi"] <= 0:
            return
        e["ft"] -= dt
        if e["ft"] > 0:
            return
        if e["y"] > C["WorldHeight"] - 0.5 or e["y"] < 1:
            e["ft"] = e["fi"]
            return
        e["ft"] = e["fi"] * self.rng.uniform(0.9, 1.1)
        s = min(C["EnemyBulletSpeedBase"] + C["EnemyBulletSpeedGain"] * self.difficulty,
                C["EnemyBulletSpeedMax"])
        if e["kind"] == 2:
            s *= C["EnemyBulletSpeedLargeMul"]
        mx, my = e["x"], e["y"] - C["EnemyHalfH"][e["kind"]] - 0.1
        self.ebullets.append([mx, my, 0.0, -s])
        if e["kind"] == 3:
            self.ebullets.append([mx - 0.4, my, -s * 0.45, -s])
            self.ebullets.append([mx + 0.4, my, s * 0.45, -s])

    @staticmethod
    def overlap(ax, ay, ahw, ahh, bx, by, bhw, bhh):
        return abs(ax - bx) < ahw + bhw and abs(ay - by) < ahh + bhh

    def collide(self):
        # 玩家子弹 × 敌机
        for b in self.pbullets[:]:
            for e in self.enemies[:]:
                k = e["kind"]
                if not self.overlap(b[0], b[1], C["PlayerBulletHalfW"], C["PlayerBulletHalfH"],
                                    e["x"], e["y"], C["EnemyHalfW"][k], C["EnemyHalfH"][k]):
                    continue
                self.pbullets.remove(b)
                e["hp"] -= C["PlayerBulletDamage"]
                if e["hp"] <= 0:
                    self.enemies.remove(e)
                    self.kill(e, True)
                break
        if not self.alive:
            return
        # 敌弹 × 玩家
        for b in self.ebullets[:]:
            if self.overlap(b[0], b[1], C["EnemyBulletHalfW"], C["EnemyBulletHalfH"],
                            self.px, self.py, C["PlayerHalfW"] * 0.85, C["PlayerHalfH"] * 0.85):
                self.ebullets.remove(b)
                self.damage()
                if not self.alive:
                    return
        # 撞击
        for e in self.enemies[:]:
            k = e["kind"]
            if self.overlap(e["x"], e["y"], C["EnemyHalfW"][k] * 0.8, C["EnemyHalfH"][k] * 0.8,
                            self.px, self.py, C["PlayerHalfW"] * 0.8, C["PlayerHalfH"] * 0.8):
                self.damage()
                e["hp"] -= 2
                if e["hp"] <= 0:
                    self.enemies.remove(e)
                    self.kill(e, False)
                if not self.alive:
                    return
        # 道具
        for p in self.powerups[:]:
            if self.overlap(p[0], p[1], C["PowerupHalf"], C["PowerupHalf"],
                            self.px, self.py, C["PlayerHalfW"] + 0.15, C["PlayerHalfH"] + 0.15):
                self.powerups.remove(p)
                self.apply_powerup(p[2])

    def kill(self, e, scored):
        k = e["kind"]
        if scored:
            self.score += C["EnemyScore"][k]
            self.kills += 1
        self.events.append(("kill", k))
        if scored:
            self.roll_drops(e)

    def roll_drops(self, e):
        col = {1: 0, 2: 1, 3: 2}.get(e["kind"])
        if col is None:
            return
        for row in range(3):
            ch = C["DropChance"][row][col]
            if ch > 0 and self.rng.random() < ch:
                self.powerups.append([min(max(e["x"], 0.6), C["WorldWidth"] - 0.6), e["y"], row])

    def apply_powerup(self, kind):
        self.pickups[kind] += 1
        if kind == 0:
            self.double_left = C["DoubleShotDuration"]
        elif kind == 1:
            for e in self.enemies[:]:
                if e["kind"] == 3:
                    e["hp"] -= int(C["BombDamageToBoss"])
                    if e["hp"] <= 0:
                        self.enemies.remove(e)
                        self.kill(e, True)
                else:
                    self.enemies.remove(e)
                    self.kill(e, True)
            self.ebullets.clear()
        elif kind == 2:
            if self.lives < C["PlayerMaxLives"]:
                self.lives += 1

    def damage(self):
        if not self.alive:
            return
        self.lives -= 1
        if self.lives <= 0:
            self.lives = 0
            self.alive = False
            self.dead_timer = C["PlayerDeathDuration"]
            self.events.append(("dead", 0))


# ------------------------- 场景测试 -------------------------

def run(seconds, move_policy=None, seed=1):
    sim = Sim(seed)
    sim.begin()
    steps = int(seconds / C["Tick"])
    for i in range(steps):
        if sim.phase != "playing":
            break
        if move_policy:
            move_policy(sim)
        sim.step()
        sim.events.clear()
    return sim


def test_afk_dies():
    """站桩不动:应当被敌机/敌弹打死并进入 GameOver。"""
    sim = run(120, seed=7)
    assert sim.phase == "gameover", f"站桩 120s 未死亡(phase={sim.phase}, lives={sim.lives})"
    assert sim.score > 0 and sim.kills >= 0
    print(f"  [OK] 站桩玩家死亡: t={sim.time:.1f}s score={sim.score} kills={sim.kills}")


def test_evade_survives():
    """躲避策略:按威胁距离场选横向安全点。验证可玩性(能活/能杀/能捡)。"""
    def policy(sim):
        # 威胁 = 屏幕上方的敌机 + 下落中的敌弹(按其到达玩家高度的时间加权)
        pts = []
        for e in sim.enemies:
            if e["y"] < 14:
                pts.append((e["x"], e["y"]))
        for b in sim.ebullets:
            if b[1] < 12:
                pts.append((b[0], b[1]))
        best_x, best_v = sim.px, -1
        for cand_x in [x * 0.3 for x in range(3, 28)]:
            if not (C["PlayerMinX"] <= cand_x <= C["PlayerMaxX"]):
                continue
            v = min((math.hypot(cand_x - tx, 2.4 - ty) for tx, ty in pts), default=99)
            # 轻微偏好中心(保持火力覆盖)
            v -= abs(cand_x - C["WorldWidth"] / 2) * 0.05
            if v > best_v:
                best_v, best_x = v, cand_x
        sim.set_target(best_x, 2.4)
    sim = run(240, policy, seed=3)
    assert sim.time > 60, f"躲避策略存活时间过短: {sim.time:.1f}s"
    assert sim.kills > 20, f"击杀过少: {sim.kills}"
    assert sim.score > 300, f"得分过低: {sim.score}"
    print(f"  [OK] 躲避玩家: t={sim.time:.1f}s phase={sim.phase} score={sim.score} "
          f"kills={sim.kills} lives={sim.lives} pickups={sim.pickups}")


def test_boss_appears():
    """10 波一循环,第 10 波含 BOSS:约 1 分钟内应见到。"""
    seen = {"boss": False}
    sim = Sim(5)
    sim.begin()
    for _ in range(int(150 / C["Tick"])):
        if sim.phase != "playing":
            break
        sim.step()
        for e in sim.enemies:
            if e["kind"] == 3:
                seen["boss"] = True
        sim.events.clear()
    assert seen["boss"], "150 秒内未出现 BOSS"
    print("  [OK] BOSS 按波次表出现")


def _evade_policy(sim):
    import math as _m
    pts = [(e["x"], e["y"]) for e in sim.enemies if e["y"] < 14]
    pts += [(b[0], b[1]) for b in sim.ebullets if b[1] < 12]
    best_x, best_v = sim.px, -1
    for cand_x in [x * 0.3 for x in range(3, 28)]:
        if not (C["PlayerMinX"] <= cand_x <= C["PlayerMaxX"]):
            continue
        v = min((_m.hypot(cand_x - tx, 2.4 - ty) for tx, ty in pts), default=99)
        v -= abs(cand_x - C["WorldWidth"] / 2) * 0.05
        if v > best_v:
            best_v, best_x = v, cand_x
    sim.set_target(best_x, 2.4)


def test_difficulty_ramp():
    """难度随时间上升到 1(上帝模式:死后立即复活,只验证进度系统)。"""
    sim = Sim(9)
    sim.begin()
    d0 = None
    max_d = 0.0
    max_level = 1
    for i in range(int(600 / C["Tick"])):
        if sim.phase != "playing":
            break
        _evade_policy(sim)
        sim.step()
        # 上帝模式:死亡立即复活,保证跑到难度上限
        if not sim.alive:
            sim.alive = True
            sim.lives = 3
        if d0 is None and sim.time > 1:
            d0 = sim.difficulty
        max_d = max(max_d, sim.difficulty)
        max_level = max(max_level, sim.level)
        sim.events.clear()
    assert d0 is not None and d0 < 0.1, "开局难度应接近 0"
    assert max_d > 0.95, f"600 秒内应达到满难度,实际最高 {max_d}"
    assert max_level >= 9, f"关卡应随时间推进,实际最高 {max_level}"
    print(f"  [OK] 难度曲线: start≈{d0:.2f} → 最高 {max_d:.2f}(关卡 {max_level},t={sim.time:.0f}s)")


def test_restart():
    sim = Sim(11)
    sim.begin()
    for _ in range(600):
        sim.step()
    score1 = sim.score
    # 镜像 Restart
    sim.__init__(11)
    sim.rng = random.Random(11 ^ 0x9E3779B9)
    sim.begin()
    assert sim.score == 0 and sim.lives == C["PlayerStartLives"]
    for _ in range(600):
        sim.step()
    print(f"  [OK] 重开后状态清零(第一局 10s 得分 {score1})")


def test_wave_counts():
    """前 3 波(0-9.5s)应生成 5+4+2=11 架敌机。"""
    sim = Sim(13)
    sim.begin()
    spawned = 0
    seen = set()
    for _ in range(int(11 / C["Tick"])):
        before = len(sim.enemies)
        sim.step()
        spawned += max(0, len(sim.enemies) - before)
        for e in sim.enemies:
            seen.add(e["id"])
        sim.events.clear()
    assert len(seen) == 11, f"前 11 秒应出现 11 架敌机,实际 {len(seen)}"
    print("  [OK] 波次数量:前 11 秒共生成 11 架(5+4+2)")


if __name__ == "__main__":
    print("== 玩法逻辑仿真测试(数值来自 GameConfig.cs) ==")
    test_wave_counts()
    test_afk_dies()
    test_evade_survives()
    test_boss_appears()
    test_difficulty_ramp()
    test_restart()
    print("== 全部通过 ==")
