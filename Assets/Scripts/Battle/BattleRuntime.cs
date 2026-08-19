using System.Collections;
using System.Collections.Generic;
using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// Owns the live battle: spawns bricks and enemies, resolves damage,
    /// drives the wave timeline, decides win/lose.
    public class BattleRuntime : MonoBehaviour
    {
        [Header("Scene wiring (filled by SceneBuilder)")]
        public Transform BrickRoot;
        public Transform EnemyRoot;
        public Transform FxRoot;
        public Transform Ground;

        [Header("Level")]
        public int SceneId = 1;
        public bool Hard = false;

        [Header("Layout")]
        public float GroundY = 0f;
        [Tooltip("How close to the pedestal's edge an enemy has to get before " +
                 "it can reach the base. Flyers pass over the stack entirely, " +
                 "so contact with a brick cannot be the only way in.")]
        public float BaseReach = 0.4f;

        public BattleContext Ctx { get; private set; } = new BattleContext();

        readonly List<EnemyUnit> _enemies = new List<EnemyUnit>();
        readonly List<BrickUnit> _bricks = new List<BrickUnit>();

        public IReadOnlyList<BrickUnit> Bricks => _bricks;
        public int AliveEnemies => _enemies.Count;

        BrickDropper _dropper;
        ChallengeModifiers _challenges;
        Coroutine _waveLoop;

        /// The starting pedestal. Built here rather than baked into the scene
        /// so its width can follow the map row.
        public BasePlatform Base { get; private set; }

        /// The HP pool every monster is actually attacking.
        public BaseHealth BaseHp { get; private set; }

        public System.Action<string> OnLog;

        void Awake()
        {
            // Without this the editor stops ticking Play the moment the Game
            // view loses focus, which freezes both the battle and the editor's
            // command queue -- automated probing then reads a stalled world.
            Application.runInBackground = true;

            _dropper = GetComponent<BrickDropper>();
            _challenges = GetComponent<ChallengeModifiers>();
        }

        /// Config has to be loaded before StartBattle reads a map row, and on
        /// Android that load is a coroutine, so Start is an iterator and the
        /// battle begins a few frames in.
        ///
        /// This used to load synchronously in Awake. That works in the Editor
        /// and on desktop and fails on Android, where StreamingAssets is not a
        /// real directory -- the APK boots, renders the camera's clear colour,
        /// and shows nothing, because Awake disabled the runtime before
        /// anything was built. Nothing else reports an error, so the only
        /// symptom is an empty screen. See ConfigDB.LoadAny.
        IEnumerator Start()
        {
            if (!ConfigDB.Loaded)
            {
                string failure = null;
                yield return ConfigDB.LoadAny(e => failure = e);

                if (failure != null || !ConfigDB.Loaded)
                {
                    Debug.LogError($"[Battle] config load failed: " +
                                   $"{failure ?? "tables did not populate"} " +
                                   $"(streamingAssetsPath={Application.streamingAssetsPath})");
                    enabled = false;
                    yield break;
                }
            }

            StartBattle(SceneId, Hard);
        }

        public void StartBattle(int sceneId, bool hard)
        {
            var map = hard
                ? ConfigDB.MapsHard.Find(m => m.ID == sceneId)
                : ConfigDB.Map(sceneId);

            if (map == null)
            {
                Debug.LogError($"[Battle] no map row for scene {sceneId} (hard={hard})");
                return;
            }

            // Meta attributes would come from equipped collection cards; the
            // demo starts with none so the raw table numbers are visible.
            Ctx.Setup(map, hard, new AttrSet());
            _challenges?.Apply(Ctx);

            // The pedestal must exist before the dropper starts, so the first
            // brick has something to land on.
            BuildBase(map);

            _dropper?.Begin(this, Ctx);
            BindCamera();

            Ctx.SetPhase(BattlePhase.Running);
            Log($"{map.Title} {map.ChapterTitle} | waves={Ctx.TotalWaves} " +
                $"gold={Ctx.Gold} bounds=[{Ctx.LeftBound},{Ctx.RightBound}] " +
                $"desk={map.Desk} base({Base}) killGold={Ctx.KillReward(false)}/{Ctx.KillReward(true)}");

            if (_waveLoop != null) StopCoroutine(_waveLoop);
            _waveLoop = StartCoroutine(RunWaves());
        }

        /// Added here rather than in the scene asset so a regenerated scene
        /// keeps working without extra wiring.
        void BindCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rig = cam.GetComponent<TowerCamera>();
            if (rig == null) rig = cam.gameObject.AddComponent<TowerCamera>();
            rig.Bind(this);
        }

        void BuildBase(BrickMapRow map)
        {
            if (Base == null)
            {
                var go = new GameObject("Base");
                go.transform.SetParent(BrickRoot != null ? BrickRoot.parent : transform, false);
                Base = go.AddComponent<BasePlatform>();
            }
            Base.Build(map.Desk, Base.transform.parent, GroundY, 0f);

            if (BaseHp == null) BaseHp = Base.gameObject.AddComponent<BaseHealth>();
            BaseHp.Setup(this, Ctx);
        }

        /// Re-totals the bricks' HP contributions into the pool. Called on every
        /// change to the standing set instead of tracking a running sum, which
        /// would drift the first time a merge or a ground loss slipped through.
        public void RefreshBaseBonus()
        {
            if (BaseHp == null) return;
            double sum = 0.0;
            for (int i = 0; i < _bricks.Count; i++)
            {
                var b = _bricks[i];
                if (b == null || !b.IsAlive) continue;
                sum += b.HpBonus;
            }
            BaseHp.SetBrickBonus(sum);
        }

        /// Monsters hit the pool, not the brick they happen to be standing next
        /// to -- see BaseHealth for why the tables say so.
        public void DamageBase(double amount)
        {
            if (Ctx.Phase != BattlePhase.Running || BaseHp == null) return;
            BaseHp.TakeDamage(amount);
        }

        public void OnBaseDestroyed()
        {
            if (Ctx.Phase != BattlePhase.Running) return;
            Ctx.SetPhase(BattlePhase.Lost);
            Log("defeat: base destroyed");
        }

        /// True once an enemy is close enough to the pedestal to chew on it.
        public bool CanReachBase(float x) =>
            Base != null && Mathf.Abs(x - Base.CenterX) <= Base.HalfWidth + BaseReach;

        // --- wave timeline -------------------------------------------------
        IEnumerator RunWaves()
        {
            var waves = ConfigDB.ScenWaves(Ctx.Map.ID, Ctx.Hard);

            for (int i = 0; i < waves.Count; i++)
            {
                if (Ctx.Phase != BattlePhase.Running) yield break;

                var row = waves[i];
                Ctx.WaveIndex = i;
                Ctx.NotifyWaveStarted(row.Wave);
                Log($"wave {row.Wave}/{waves.Count} type={row.WaveType}");

                yield return RunWave(row);

                // Wait for the field to clear before paying out and advancing.
                while (_enemies.Count > 0 && Ctx.Phase == BattlePhase.Running)
                    yield return null;

                if (Ctx.Phase != BattlePhase.Running) yield break;

                Ctx.PayWaveReward();
                if (Ctx.ChestWaves.Contains(row.Wave)) Log($"chest at wave {row.Wave}");
            }

            Ctx.SetPhase(BattlePhase.Won);
            Log("victory");
        }

        /// Each group in Refresh is an independent spawn track on the wave's
        /// own timeline: StartTime is an offset from the wave start, verified
        /// monotonic across all 6962 rows.
        IEnumerator RunWave(BrickMonsterRow row)
        {
            var groups = RefreshGroup.ParseAll(row.Refresh);
            if (groups.Count == 0) yield break;

            int running = 0;
            foreach (var g in groups)
            {
                running++;
                StartCoroutine(RunGroup(g, () => running--));
            }
            // Hand control back once every track has finished emitting.
            while (running > 0 && Ctx.Phase == BattlePhase.Running) yield return null;
        }

        IEnumerator RunGroup(RefreshGroup g, System.Action done)
        {
            if (g.StartTime > 0f) yield return new WaitForSeconds(g.StartTime);

            for (int i = 0; i < g.Count; i++)
            {
                if (Ctx.Phase != BattlePhase.Running) break;
                SpawnEnemy(g);
                if (g.SpawnInterval > 0f) yield return new WaitForSeconds(g.SpawnInterval);
            }
            done?.Invoke();
        }

        void SpawnEnemy(RefreshGroup g)
        {
            var row = ConfigDB.Enemy(g.EnemyId);
            if (row == null) return;

            // Alternate the approach side so the tower is pressured from both.
            int dir = Random.value < 0.5f ? -1 : 1;
            // The offscreen margin and MoveY are both source-unit lengths.
            float margin = 1.5f * RefScale.FromSource;
            float x = dir < 0 ? Ctx.RightBound + margin : Ctx.LeftBound - margin;
            float y = GroundY + (0.4f + row.MoveY) * RefScale.FromSource;

            var go = Prefabs.MakeEnemy(row, EnemyRoot);
            go.transform.position = new Vector3(x, y, 0f);

            var unit = go.GetComponent<EnemyUnit>();
            double hp = g.Hp * _challengeHpMul;
            unit.Init(row, hp, g.Attack, dir, this);
            _enemies.Add(unit);
        }

        float _challengeHpMul = 1f;
        public void SetChallengeHpMultiplier(float m) => _challengeHpMul = m;

        // --- tower height --------------------------------------------------
        /// Highest point of the standing tower, and the height buff it earns.
        /// Recomputed once a frame here rather than per brick: every brick asks
        /// for the multiplier when it fires, and they would all compute the
        /// same answer.
        public float TowerTopY { get; private set; }
        public float TowerMetres { get; private set; }
        public float HeightAttackMul { get; private set; } = 1f;

        [Header("Placement")]
        [Tooltip("How far above the stack the white line sits. A piece has to " +
                 "be released above it, so bricks land on the tower instead of " +
                 "being inserted into it.")]
        public float DropLineClearance = BrickShape.CellSize;

        /// The white line: the lowest height a piece may be released at.
        public float DropLineY => TowerTopY + DropLineClearance;

        void Update() => RecomputeHeight();

        void RecomputeHeight()
        {
            float top = Base != null ? Base.TopY : GroundY;
            for (int i = 0; i < _bricks.Count; i++)
            {
                var b = _bricks[i];
                if (b == null || !b.IsAlive || b.Lost) continue;
                // Only resting bricks count -- a piece still falling would let
                // the buff (and the white line) spike while it is in the air.
                if (!b.Settled) continue;
                float y = b.Bounds.max.y;
                if (y > top) top = y;
            }
            TowerTopY = top;
            TowerMetres = HeightBonus.Metres(top - (Base != null ? Base.TopY : GroundY), 0f);
            HeightAttackMul = HeightBonus.AttackMul(TowerMetres);
        }

        // --- queries -------------------------------------------------------
        public EnemyUnit FindNearestEnemy(Vector2 from)
        {
            EnemyUnit best = null;
            float bestSqr = float.MaxValue;
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var e = _enemies[i];
                if (e == null || !e.IsAlive) { _enemies.RemoveAt(i); continue; }
                float d = ((Vector2)e.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = e; }
            }
            return best;
        }

        // --- combat --------------------------------------------------------
        public void SpawnProjectile(BrickUnit src, EnemyUnit target, DamageRoll roll,
                                    float radius, int extraHits, int skillType, float burst)
        {
            var skill = (SkillType)skillType;

            // Support skills never travel -- they resolve on the caster.
            if (skill == SkillType.Repair) { DoRepair(src, roll.Amount); return; }
            if (skill == SkillType.Miner) { DoMine(); return; }

            var go = Prefabs.MakeProjectile(skill, FxRoot);
            go.transform.position = src.Position;
            go.GetComponent<Projectile>().Launch(this, target, roll, radius, extraHits, skill);
        }

        public void ApplyImpact(Vector2 at, EnemyUnit primary, DamageRoll roll,
                                float radius, int extraHits, SkillType skill)
        {
            switch (skill)
            {
                case SkillType.Poison:
                    // DmgCount ticks spread over time rather than one burst.
                    if (primary != null && primary.IsAlive)
                        StartCoroutine(PoisonTicks(primary, roll.Amount, Mathf.Max(1, extraHits)));
                    break;

                case SkillType.Freeze:
                    HitArea(at, Mathf.Max(radius, 0.6f), roll, e => e.ApplySlow(0.5f, 2f));
                    break;

                case SkillType.Ninja:
                    // Rapid strikes: the extra hits land immediately.
                    for (int i = 0; i <= extraHits; i++)
                        if (primary != null && primary.IsAlive) primary.TakeDamage(roll.Amount, roll.Critical);
                    break;

                case SkillType.Lob:
                    HitArea(at, Mathf.Max(radius, 0.5f), roll, null);
                    if (extraHits > 0) StartCoroutine(GroundBurn(at, Mathf.Max(radius, 1f), roll.Amount * 0.25f, extraHits));
                    break;

                default:
                    if (radius > 0f) HitArea(at, radius, roll, null);
                    else if (primary != null && primary.IsAlive) primary.TakeDamage(roll.Amount, roll.Critical);
                    break;
            }
        }

        void HitArea(Vector2 at, float radius, DamageRoll roll, System.Action<EnemyUnit> extra)
        {
            float sqr = radius * radius;
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var e = _enemies[i];
                if (e == null || !e.IsAlive) continue;
                if (((Vector2)e.transform.position - at).sqrMagnitude > sqr) continue;
                extra?.Invoke(e);
                e.TakeDamage(roll.Amount, roll.Critical);
            }
        }

        IEnumerator PoisonTicks(EnemyUnit target, double perTick, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                if (target == null || !target.IsAlive) yield break;
                target.TakeDamage(perTick, false);
                yield return new WaitForSeconds(0.5f);
            }
        }

        IEnumerator GroundBurn(Vector2 at, float radius, double perTick, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                HitArea(at, radius, new DamageRoll { Amount = perTick }, null);
                yield return new WaitForSeconds(0.5f);
            }
        }

        /// 修理工: "随身携带工具箱，能快速修复基地" -- the heal goes to the base
        /// pool, and its size is an ordinary attack roll, so the height buff
        /// reaches it the same way damage does.
        void DoRepair(BrickUnit caster, double amount)
        {
            if (BaseHp != null) BaseHp.Heal(amount);
        }

        /// The miner pays gold instead of damage. MinerGold attribute adds to
        /// the per-tick payout; Global.MinerGoldCap bounds it.
        void DoMine()
        {
            int cap = ConfigDB.Global != null && ConfigDB.Global.MinerGoldCap > 0
                ? ConfigDB.Global.MinerGoldCap
                : 125;
            int payout = 10 + Mathf.RoundToInt(Ctx.Attrs.Get(AttrId.MinerGold));
            Ctx.AddGold(Mathf.Min(cap, payout));
        }

        // --- registry / outcomes -------------------------------------------
        public void RegisterBrick(BrickUnit b)
        {
            if (b == null || _bricks.Contains(b)) return;
            _bricks.Add(b);
            // A 城墙 has to raise the pool the moment it is placed.
            RefreshBaseBonus();
        }

        /// Any exit from the standing set: demolished, merged away, or dropped
        /// on the floor. It is no longer a defeat check -- the run ends when the
        /// base pool empties, not when the tower does -- so all three callers
        /// land here.
        public void OnBrickDestroyed(BrickUnit b)
        {
            _bricks.Remove(b);
            // Losing a wall costs the base the HP it was lending.
            RefreshBaseBonus();
        }

        public int BricksLostToGround { get; private set; }

        public void NotifyBrickLost(BrickUnit b)
        {
            BricksLostToGround++;
            if (b != null && b.Row != null)
                Log($"brick lost: {b.Row.Name} {b.Shape} hit the ground (-{b.Row.Cost} gold)");
        }

        public void OnEnemyKilled(EnemyUnit e)
        {
            _enemies.Remove(e);

            // Kills fund the tower. This is the loop the whole game runs on:
            // gold from kills buys the next brick, which kills faster. Paying
            // only at the end of a wave leaves the player unable to react to
            // the wave they are actually in.
            if (e != null) Ctx.PayKillReward(e.IsBoss);
        }

        public void SpawnDamagePopup(Vector2 at, double amount, bool crit) { /* hook for FX */ }

        void Log(string msg)
        {
            Debug.Log("[Battle] " + msg);
            OnLog?.Invoke(msg);
        }
    }
}
