using System.Collections.Generic;
using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// A placed hero brick. Physical: it falls, stacks, and can be toppled by
    /// wind or a gravity modifier -- which is what makes the tower a structure
    /// the player has to keep standing rather than a grid of turrets.
    ///
    /// Its footprint is a polyomino (see BrickShape), built as one rigidbody
    /// with a box collider per cell.
    [RequireComponent(typeof(Rigidbody2D))]
    public class BrickUnit : MonoBehaviour
    {
        public BrickHeroRow Row { get; private set; }
        public BrickShape Shape { get; private set; }
        public int Level { get; private set; } = 1;
        public int MergeSkin => Row != null ? Row.MergeSkin : 0;
        public SkillType Skill => Row != null ? (SkillType)Row.SkillType : SkillType.Wall;

        /// What this brick lends the base's HP pool -- see BaseHealth for why
        /// BrickHeroLevel.Hp is a contribution and not a durability.
        public double HpBonus { get; private set; }

        bool _demolished;
        public bool IsAlive => !_demolished && !Lost;
        public Vector2 Position => transform.position;

        public Rigidbody2D Body { get; private set; }

        BattleContext _ctx;
        BattleRuntime _runtime;
        float _fireTimer;
        double _attack;
        float _critRate, _critDamage, _radius;
        int _damageCount;
        SpriteRenderer[] _art;
        FigureAnimator _hero;
        Collider2D[] _cols;

        /// Settled means the brick has come to rest and may start firing.
        /// Firing while tumbling would let a dropped brick snipe mid-air.
        public bool Settled { get; private set; }
        float _settleTimer;

        /// >= 0 once the piece has been written off (missed the pedestal); it
        /// fades out over DissolveTime instead of vanishing on the same frame.
        const float DissolveTime = 0.25f;
        float _dissolve = -1f;
        public bool Lost => _dissolve >= 0f;

        public void Init(BrickHeroRow row, BrickShape shape, int level,
                         BattleContext ctx, BattleRuntime runtime)
        {
            Row = row;
            Shape = shape;
            Level = Mathf.Max(1, level);
            _ctx = ctx;
            _runtime = runtime;

            Body = GetComponent<Rigidbody2D>();
            _art = GetComponentsInChildren<SpriteRenderer>();
            _hero = GetComponentInChildren<FigureAnimator>();
            _cols = GetComponents<Collider2D>();

            if (_hero != null)
            {
                _hero.LoadClips(Figures.HeroPath(Row.ID), false);
                _hero.Play(FigureClip.Appear);
            }

            RecalcStats();
            _fireTimer = Row.BurstTime;
            ApplyTint();
        }

        /// World-space bounds of every cell. Merge tests overlap against this
        /// rather than a centre-to-centre radius: with polyomino footprints the
        /// centres of two touching pieces can be several units apart.
        public Bounds Bounds
        {
            get
            {
                if (_cols == null || _cols.Length == 0)
                    return new Bounds(transform.position, Vector3.one * BrickShape.CellSize);
                var b = _cols[0].bounds;
                for (int i = 1; i < _cols.Length; i++) b.Encapsulate(_cols[i].bounds);
                return b;
            }
        }

        void RecalcStats()
        {
            var lv = ConfigDB.HeroLevel(Row.ID, Level);
            double baseAtk = lv != null ? lv.Attack : 0.0;
            double baseHp = lv != null ? lv.Hp : 0.0;

            // Level unlocks contribute their own attribute mods on top of the
            // globally accumulated set (cards, rogue picks).
            var local = new AttrSet();
            local.Add(_ctx.Attrs);
            for (int l = 1; l <= Level; l++)
            {
                var r = ConfigDB.HeroLevel(Row.ID, l);
                if (r != null && !string.IsNullOrEmpty(r.Attrs))
                    local.Add(Parse.Attrs(r.Attrs));
            }

            _attack = baseAtk * local.AttackMul;
            // Hp is what the brick lends the base, so it passes straight
            // through: no area scaling, because the footprint is the hero's own
            // fixed shape and the table value is already per brick.
            HpBonus = baseHp * local.HpMul;

            _critRate = Row.CriticalRate + local.CritRateAdd;
            _critDamage = Row.CriticalDamage + local.CritDamageAdd;
            _radius = Row.DmgRadius * local.RadiusMul;
            _damageCount = Row.DmgCount + local.ExtraDamageCount;

            _fireInterval = Row.Interval > 0f
                ? Row.Interval / Mathf.Max(0.01f, local.AttackSpeedMul)
                : 0f;
        }

        float _fireInterval;

        void FixedUpdate()
        {
            if (Lost) return;
            if (!Settled)
            {
                // Rest detection: low velocity for a short continuous window.
                if (Body.velocity.sqrMagnitude < 0.01f && Mathf.Abs(Body.angularVelocity) < 5f)
                {
                    _settleTimer += Time.fixedDeltaTime;
                    if (_settleTimer >= 0.15f) Settled = true;
                }
                else _settleTimer = 0f;
            }
        }

        /// Touching the ground means the piece slid off the pedestal. The
        /// tower is only what stands on the base, so a grounded brick is spent
        /// -- gold and all. Without this the stack simply spreads sideways
        /// along the floor and placement stops being a decision.
        void OnCollisionEnter2D(Collision2D c)
        {
            if (Lost || _runtime == null || _runtime.Ground == null) return;
            var t = c.collider.transform;
            if (t == _runtime.Ground || t.IsChildOf(_runtime.Ground)) LoseToGround();
        }

        void LoseToGround()
        {
            if (Lost) return;
            _dissolve = 0f;
            Settled = false;

            // Stop interacting immediately: anything resting on it should fall
            // now, not when the fade finishes.
            if (_cols != null)
                foreach (var col in _cols) if (col != null) col.enabled = false;
            if (Body != null) Body.simulated = false;

            _runtime.OnBrickDestroyed(this);
            _runtime.NotifyBrickLost(this);
        }

        void Update()
        {
            if (Lost) { Fade(); return; }

            if (_ctx == null || _ctx.Phase != BattlePhase.Running) return;
            if (!IsAlive || !Settled) return;
            if (Skill == SkillType.Wall || _fireInterval <= 0f) return;   // wall

            _fireTimer -= Time.deltaTime;
            if (_fireTimer > 0f) return;
            _fireTimer = _fireInterval;

            // The support bricks are the reason this is not just a turret grid:
            // the repairman and the miner produce with no enemy on the field,
            // so they have to tick without a target to aim at.
            if (NeedsTarget)
            {
                var target = _runtime.FindNearestEnemy(Position);
                if (target != null) Fire(target);
            }
            else Fire(null);
        }

        bool NeedsTarget => Skill != SkillType.Repair && Skill != SkillType.Miner;

        void Fade()
        {
            _dissolve += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_dissolve / DissolveTime);
            if (_art != null)
                foreach (var sr in _art)
                {
                    if (sr == null) continue;
                    var c = sr.color; c.a = k; sr.color = c;
                }
            // The figure's colour is the animator's to write, so the fade has to
            // go through it or it would be overwritten on the next frame.
            if (_hero != null) _hero.Alpha = k;
            transform.localScale *= 1f - Time.deltaTime * 1.5f;
            if (_dissolve >= DissolveTime) Destroy(gameObject);
        }

        void Fire(EnemyUnit target)
        {
            // The height buff multiplies in here, not in RecalcStats: the tower
            // grows and loses layers constantly, and every brick should feel the
            // current ruler band rather than the one it was placed in.
            double atk = _attack * (_runtime != null ? _runtime.HeightAttackMul : 1f);
            // Lunge toward whatever is being shot at; the support roles have no
            // target, so they just swing on the spot.
            if (_hero != null)
            {
                var aim = target != null ? (target.Position - Position) : Vector2.right;
                _hero.Play(FigureClip.Attack, aim);
            }
            var roll = Crit.Roll(atk, _critRate, _critDamage);
            _runtime.SpawnProjectile(this, target, roll, _radius, _damageCount, Row.SkillType, Row.BurstTime);
        }

        /// Take this brick out of the tower for good.
        ///
        /// Nothing in the source data damages a placed brick: the two things
        /// that remove one are the hammer prop (item 15, "摧毁一个方块") and the
        /// 禁空领域 height cap, and both are outright removals. That is the
        /// other half of the argument in BaseHealth for bricks carrying no HP.
        public void Demolish()
        {
            if (_demolished || Lost) return;
            _demolished = true;
            _runtime.OnBrickDestroyed(this);
            Destroy(gameObject);
        }

        /// Merge is destructive on the source: two same-MergeSkin bricks become
        /// one at level+1, which is what the cube_collision -> cube_upgrade
        /// sound pair in the original implies. The survivor keeps its own
        /// footprint -- the levelled-up piece is denser, not bigger.
        public void MergeFrom(BrickUnit other)
        {
            Level += 1;
            RecalcStats();
            transform.localScale *= 1.04f;

            // The rim is the level readout in the original art, so a merge has
            // to swap it -- and the renderer list has to be retaken, because
            // the swap replaced one of the children.
            BrickArt.UpdateRim(transform, Row, Shape, Level, ArtOrder + 1, 1f);
            _art = GetComponentsInChildren<SpriteRenderer>();
            _hero = GetComponentInChildren<FigureAnimator>();
            ApplyTint();
            if (_hero != null) _hero.Play(FigureClip.Appear);
            // The level-up raised HpBonus for the three support roles, so the
            // pool has to be told.
            if (_runtime != null) _runtime.RefreshBaseBonus();
        }

        /// Sorting order Prefabs gives a standing brick's face.
        internal const int ArtOrder = 5;

        void ApplyTint()
        {
            if (_art == null) return;
            var c = BrickColor(Row);
            // Brighten with level instead of darkening with damage: a merged
            // brick is the only state change a placed brick now has.
            float lift = Mathf.Min(1.35f, 1f + (Level - 1) * 0.06f);
            var face = c * lift;
            face.a = 1f;
            var glyph = RoleColor(Row) * 1.25f;

            for (int i = 0; i < _art.Length; i++)
            {
                var sr = _art[i];
                if (sr == null) continue;
                switch (sr.gameObject.name)
                {
                    // The tile images carry their own colour, so they are only
                    // brightened. Tinting them by quality the way the flat
                    // squares are tinted would wash the artwork out.
                    case "Face":
                    case "Rim":
                        sr.color = new Color(lift, lift, lift, 1f);
                        break;
                    // The rim a merge just replaced, still alive until the end
                    // of the frame. Left as it is so it does not flash.
                    case "RimSpent":
                        break;
                    // The hero figure is real artwork now, so it is brightened
                    // like the tile images rather than tinted by role -- the
                    // role colour was the placeholder pip's only identity.
                    // Written through the animator, which owns the colour.
                    case "Hero":
                        if (_hero != null) _hero.Tint = new Color(lift, lift, lift, 1f);
                        else sr.color = glyph;
                        break;
                    default:
                        sr.color = face;
                        break;
                }
            }
        }

        public static Color QualityColor(int quality)
        {
            switch (quality)
            {
                case 1: return new Color(0.62f, 0.72f, 0.85f);   // common
                case 2: return new Color(0.55f, 0.82f, 0.55f);   // rare
                case 3: return new Color(0.85f, 0.68f, 0.35f);   // epic
                default: return Color.gray;
            }
        }

        /// The special bricks read by colour before they read by behaviour --
        /// wood for the wall, gold for the miner, green for the repairman,
        /// matching how the reference art separates them.
        public static Color RoleColor(BrickHeroRow row)
        {
            if (row == null) return Color.white;
            switch ((SkillType)row.SkillType)
            {
                case SkillType.Wall:   return new Color(0.58f, 0.40f, 0.24f);
                case SkillType.Repair: return new Color(0.35f, 0.80f, 0.62f);
                case SkillType.Miner:  return new Color(0.92f, 0.72f, 0.25f);
                default:               return new Color(0.95f, 0.95f, 1f);
            }
        }

        /// Quality still drives the body colour for ordinary fighters; the
        /// three support roles override it so they stand out in the stack.
        public static Color BrickColor(BrickHeroRow row)
        {
            if (row == null) return Color.gray;
            var q = QualityColor(row.Quality);
            switch ((SkillType)row.SkillType)
            {
                case SkillType.Wall:
                case SkillType.Repair:
                case SkillType.Miner:
                    return Color.Lerp(q, RoleColor(row), 0.7f);
                default:
                    return q;
            }
        }
    }
}
