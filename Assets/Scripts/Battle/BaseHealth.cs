using UnityEngine;

namespace Skyscraper.Battle
{
    /// The base's health pool -- the bar the reference draws across the whole
    /// bottom of the screen, and the run's actual lose condition.
    ///
    /// Why one shared pool instead of per-brick HP, which is what this project
    /// had until now: BrickHeroLevel.Hp is 0 at every level for all 13
    /// attacking heroes, and non-zero for exactly three -- 城墙 (20, +10/lv),
    /// 修理工 (20, +5/lv) and 矿工 (20, +5/lv). A brick with no attack and no
    /// durability of its own would be pointless, and 城墙's own description says
    /// what the column means: "弱小的史莱姆，可以提供少量血量" -- it *provides*
    /// HP. 修理工's says who to: "随身携带工具箱，能快速修复基地". So Hp is a
    /// contribution to the base, the HpRate card attribute (up to +50% on
    /// 雷鼠) scales that single pool, and monsters chew the pool rather than
    /// individual bricks. The old model needed an invented `Hp > 0 ? Hp : 100`
    /// fallback for 13 of 16 heroes, which was the tell that it read the column
    /// wrong.
    ///
    /// The 1000 starting value is the one number here with no table behind it.
    /// No column in any of the 28 files carries it -- BrickMap has BattleGold,
    /// Reward, Box*, Desk and the monster bounds and nothing else numeric, and
    /// the only HP-ish fields in the whole set are BrickHeroLevel.Hp and
    /// Global.ChallSwampHP (3). So it is read off the reference screenshot's bar
    /// label and kept here as a single constant to swap later.
    public class BaseHealth : MonoBehaviour
    {
        public const double DefaultMaxHp = 1000.0;

        public double Hp { get; private set; }
        public double MaxHp { get; private set; }

        /// The part of MaxHp the standing bricks contribute, before HpRate.
        public double BrickBonus { get; private set; }

        public bool IsAlive => Hp > 0.0;
        public float Ratio => MaxHp > 0.0 ? Mathf.Clamp01((float)(Hp / MaxHp)) : 0f;

        /// Damage taken this run -- reported by the probe so a run can be read
        /// back without watching it.
        public double DamageTaken { get; private set; }
        public double Healed { get; private set; }

        float _hpMul = 1f;
        BattleRuntime _runtime;

        public void Setup(BattleRuntime runtime, BattleContext ctx)
        {
            _runtime = runtime;
            _hpMul = ctx != null ? Mathf.Max(0.01f, ctx.Attrs.HpMul) : 1f;
            BrickBonus = 0.0;
            MaxHp = DefaultMaxHp * _hpMul;
            Hp = MaxHp;
            DamageTaken = 0.0;
            Healed = 0.0;
        }

        /// Called whenever the standing set changes. Recomputed from the brick
        /// list rather than added and subtracted per event: a merge raises one
        /// brick's level in place, and a running total drifts the first time one
        /// of those updates is missed.
        public void SetBrickBonus(double raw)
        {
            double was = MaxHp;
            BrickBonus = System.Math.Max(0.0, raw);
            MaxHp = (DefaultMaxHp + BrickBonus) * _hpMul;

            // Gaining max HP grants it: a 城墙 placed mid-wave has to help now,
            // not only after the repairman's next tick. Not once the base is
            // already down, though -- otherwise placing a wall after the loss
            // resurrects it.
            double delta = MaxHp - was;
            if (delta > 0.0 && IsAlive) Hp += delta;
            Hp = Mathf.Clamp((float)Hp, 0f, (float)MaxHp);
        }

        public void TakeDamage(double amount)
        {
            if (amount <= 0.0 || !IsAlive) return;
            Hp -= amount;
            DamageTaken += amount;
            if (Hp <= 0.0)
            {
                Hp = 0.0;
                if (_runtime != null) _runtime.OnBaseDestroyed();
            }
        }

        /// Returns what was actually restored, so the repairman can be silent
        /// when the pool is already full.
        public double Heal(double amount)
        {
            if (amount <= 0.0 || !IsAlive) return 0.0;
            double before = Hp;
            Hp = System.Math.Min(MaxHp, Hp + amount);
            Healed += Hp - before;
            return Hp - before;
        }

        public override string ToString() => $"{Hp:0}/{MaxHp:0} (bricks +{BrickBonus:0})";
    }
}
