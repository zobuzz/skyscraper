using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// Enemies walk in from a battlefield edge until the tower stops them, then
    /// chew on the base's HP pool.
    ///
    /// They no longer damage the brick they are touching: BrickHeroLevel.Hp is 0
    /// for all 13 attacking heroes, so there is nothing to damage -- see
    /// BaseHealth. A brick still *blocks*, which is what the stack is for; it
    /// just cannot be eaten through.
    ///
    /// Inference note: BrickEnemy.MoveY is read as a hover height, not a
    /// velocity. Enemy 23 has MoveY 9.5 together with IsMove 0 -- a stationary
    /// unit at altitude only makes sense as a spawn height.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyUnit : MonoBehaviour, IDamageable
    {
        public BrickEnemyRow Row { get; private set; }
        public bool IsBoss => Row != null && Row.IsBoss;
        public double Hp { get; private set; }
        public double MaxHp { get; private set; }
        public bool IsAlive => Hp > 0.0 && this != null;
        public Vector2 Position => transform.position;

        float _attack;
        float _attackTimer;
        int _dir = -1;              // -1 walks left, +1 walks right

        /// No table stores a walk rate -- BrickEnemy has IsMove as a flag only.
        /// 1.2 source units/s crosses chapter 1's 8-unit field in ~7s, which is
        /// the pace the captures show between spawn and first contact.
        public const float SourceWalkSpeed = 1.2f;
        float _speed;
        float _slowUntil;
        float _slowFactor = 1f;
        BattleRuntime _runtime;
        SpriteRenderer _sr;
        FigureAnimator _anim;
        BrickUnit _blocking;

        public void Init(BrickEnemyRow row, double hp, float attack, int dir, BattleRuntime runtime)
        {
            Row = row;
            MaxHp = Hp = hp;
            _attack = attack;
            _dir = dir;
            _runtime = runtime;
            _sr = GetComponentInChildren<SpriteRenderer>();
            _anim = GetComponentInChildren<FigureAnimator>();

            // BrickEnemy.Scale is the prefab root's localScale in the original
            // -- 17 of the 20 authored monster prefabs carry exactly their table
            // value -- so with the figures baked at the rigs' own 0.01 it passes
            // straight through. This is not a resize: the art and the collider
            // both moved a factor of 1.25 out of themselves and into here, so
            // every product is what it was (see RefScale.EnemyMeasuredWidth).
            // What changes is that the monsters are now sized relative to each
            // other the way the original sizes them, instead of every one of
            // them being the same silhouette at the same width.
            transform.localScale = Vector3.one * Mathf.Max(0.05f, row.Scale);
            if (_anim != null)
            {
                _anim.LoadClips(Figures.EnemyPath(row.Model), true);
                _anim.SetRest(row.IsMove == 0 ? FigureClip.Idle : FigureClip.Move);
                _anim.Play(FigureClip.Appear, new Vector2(dir, 0f));
            }
            _attackTimer = row.Burst;
            // WalkSpeed is a source-unit rate, so it scales with the world too,
            // otherwise the 3.125x wider field makes every enemy crawl.
            _speed = row.IsMove == 0 ? 0f : SourceWalkSpeed * RefScale.FromSource;

            var body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;              // enemies ignore the brick physics
            body.isKinematic = true;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            Tint();
        }

        public void ApplySlow(float factor, float duration)
        {
            _slowFactor = Mathf.Clamp01(1f - factor);
            _slowUntil = Time.time + duration;
        }

        void Update()
        {
            if (_runtime == null || _runtime.Ctx.Phase != BattlePhase.Running) return;
            if (!IsAlive) return;

            float slow = Time.time < _slowUntil ? _slowFactor : 1f;

            if (AtTower)
            {
                // Standing still to chew: rest on idle so the walk cycle stops.
                if (_anim != null) _anim.SetRest(FigureClip.Idle);
                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0f)
                {
                    _attackTimer = Row.Interval;
                    _runtime.DamageBase(_attack);
                    if (_anim != null) _anim.Play(FigureClip.Attack, new Vector2(_dir, 0f));
                }
                return;
            }

            if (_speed <= 0f) return;
            transform.Translate(Vector3.right * (_dir * _speed * slow * Time.deltaTime));
            if (_anim != null)
            {
                _anim.SetRest(FigureClip.Move);
                if (_anim.Current == FigureClip.Idle)
                    _anim.Play(FigureClip.Move, new Vector2(_dir, 0f));
            }
        }

        /// Stopped and in range to attack. Two ways in, because a brick in the
        /// face cannot be the only one: BrickEnemy.MoveY puts flyers (enemy 23 is
        /// at 9.5) clean over the top of the stack, and they would otherwise walk
        /// across the whole field untouched and never attack anything.
        bool AtTower
        {
            get
            {
                if (_blocking != null && _blocking.IsAlive) return true;
                _blocking = null;
                if (_touchingBase) return true;
                return _runtime.CanReachBase(transform.position.x);
            }
        }

        bool _touchingBase;

        void OnTriggerEnter2D(Collider2D other) => TryBlock(other);
        void OnTriggerStay2D(Collider2D other) { if (_blocking == null) TryBlock(other); }

        void TryBlock(Collider2D other)
        {
            var brick = other.GetComponentInParent<BrickUnit>();
            if (brick != null && brick.IsAlive) { _blocking = brick; return; }

            // The pedestal itself: a ground walker reaches its side tiles before
            // its centre clears CanReachBase's window.
            if (other.GetComponentInParent<BasePlatform>() != null) _touchingBase = true;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<BasePlatform>() != null) _touchingBase = false;
        }

        public void TakeDamage(double amount, bool critical)
        {
            if (!IsAlive) return;
            Hp -= amount;
            _runtime.SpawnDamagePopup(Position, amount, critical);

            if (Hp <= 0.0)
            {
                Hp = 0.0;
                // The bookkeeping is immediate -- gold, the kill count and the
                // wave's "field is clear" test all run off BattleRuntime's list,
                // which OnEnemyKilled removes this from -- so letting the corpse
                // linger for its death clip cannot stall the wave. The collider
                // goes first, or a dead body keeps blocking the one behind it.
                _runtime.OnEnemyKilled(this);
                var col = GetComponent<CircleCollider2D>();
                if (col != null) col.enabled = false;
                if (_anim != null)
                {
                    _anim.Play(FigureClip.Death, new Vector2(_dir, 0f));
                    Destroy(gameObject, 0.5f);
                }
                else Destroy(gameObject);
            }
            else
            {
                Tint();
                if (_anim != null) _anim.Play(FigureClip.Hit, new Vector2(_dir, 0f));
            }
        }

        /// Health readout. Brightness only when there is real artwork: the old
        /// pink/red wash was the primitive's whole identity, and applied to a
        /// baked figure it just drains the colour out of it.
        void Tint()
        {
            float k = MaxHp > 0 ? Mathf.Clamp01((float)(Hp / MaxHp)) : 1f;
            if (_anim != null)
            {
                float lift = 0.45f + 0.55f * k;
                var tint = new Color(1f, lift, lift, 1f);      // reddens as it drops
                if (IsBoss) tint *= 1.05f;
                _anim.Tint = tint;
                return;
            }
            if (_sr == null) return;
            var c = IsBoss ? new Color(0.85f, 0.3f, 0.35f) : new Color(0.75f, 0.45f, 0.6f);
            _sr.color = Color.Lerp(c * 0.4f, c, 0.35f + 0.65f * k);
        }
    }
}
