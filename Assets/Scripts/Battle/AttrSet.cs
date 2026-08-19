using System.Collections.Generic;
using Skyscraper.Config;

namespace Skyscraper.Battle
{
    /// Accumulates AttrMod values from every source that feeds a battle:
    /// equipped collection cards, hero level unlocks, and rogue picks.
    /// Rates are additive within a channel, which matches how the source data
    /// is authored (cards stack linearly: 0.03 + 0.15 + 0.30 ...).
    public class AttrSet
    {
        readonly Dictionary<AttrId, float> _v = new Dictionary<AttrId, float>();

        public void Clear() => _v.Clear();

        public void Add(AttrId id, float value)
        {
            if (id == AttrId.None) return;
            _v.TryGetValue(id, out var cur);
            _v[id] = cur + value;
        }

        public void Add(IEnumerable<AttrMod> mods, int level = 1)
        {
            if (mods == null) return;
            foreach (var m in mods) Add(m.Id, m.ValueAt(level));
        }

        public void Add(AttrSet other)
        {
            if (other == null) return;
            foreach (var kv in other._v) Add(kv.Key, kv.Value);
        }

        public float Get(AttrId id) => _v.TryGetValue(id, out var v) ? v : 0f;

        /// AttackRate and AttackRateB are two independent bonus channels in the
        /// source data (cards feed the first, hero level unlocks the second).
        /// They combine additively into one damage multiplier.
        public float AttackMul => 1f + Get(AttrId.AttackRate) + Get(AttrId.AttackRateB);
        public float HpMul => 1f + Get(AttrId.HpRate);
        public float AttackSpeedMul => 1f + Get(AttrId.AttackSpeedRate);
        public float RadiusMul => 1f + Get(AttrId.DamageRadiusRate);
        public float SlowDown => Get(AttrId.SlowDownRate);
        public int ExtraDamageCount => (int)Get(AttrId.DamageCount);
        public int InitCoin => (int)Get(AttrId.BrickInitCoin);
        public int WaveCoin => (int)Get(AttrId.BrickWaveCoin);
        public float CritRateAdd => Get(AttrId.CriticalRate);
        public float CritDamageAdd => Get(AttrId.CriticalDamage);

        public override string ToString()
        {
            var s = "";
            foreach (var kv in _v) s += $"{kv.Key}={kv.Value:0.###} ";
            return s.TrimEnd();
        }
    }
}
