using UnityEngine;

namespace Skyscraper.Battle
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        Vector2 Position { get; }
        void TakeDamage(double amount, bool critical);
    }

    public struct DamageRoll
    {
        public double Amount;
        public bool Critical;
    }

    public static class Crit
    {
        /// Base crit values live on BrickHero (0.1 / 0.5 for every hero in the
        /// shipped data); cards and level unlocks add on top.
        public static DamageRoll Roll(double baseDamage, float critRate, float critDamage)
        {
            bool crit = Random.value < critRate;
            return new DamageRoll
            {
                Amount = crit ? baseDamage * (1.0 + critDamage) : baseDamage,
                Critical = crit,
            };
        }
    }
}
