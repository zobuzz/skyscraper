using System.Collections.Generic;

namespace Skyscraper.Config
{
    /// Attribute identifiers used across BrickCard.Attr and BrickHeroLevel.Attrs.
    /// This is the full vocabulary observed in the data -- 7 card attrs and
    /// 11 hero-level attrs. Unknown strings fall through to None so a new
    /// column in a future table version degrades instead of throwing.
    public enum AttrId
    {
        None = 0,
        AttackRate,        // multiplicative attack bonus
        AttackRateB,       // second attack bonus channel (hero levels only)
        HpRate,
        AttackSpeedRate,
        CriticalRate,
        CriticalDamage,
        DamageCount,       // flat extra hits
        DamageRadiusRate,
        SlowDownRate,
        BrickInitCoin,     // battle starting gold
        BrickWaveCoin,     // gold granted per wave
        MinerGold,         // miner hero payout
        NinJiaAttack,      // ninja extra strike
    }

    public struct AttrMod
    {
        public AttrId Id;
        public float Base;     // value at level 1
        public float PerLevel; // increment per additional level

        public float ValueAt(int level) => Base + PerLevel * (level - 1);
    }

    public struct RewardEntry
    {
        public int ItemId;
        public int Count;
    }

    public static class Parse
    {
        public static AttrId ToAttrId(string s)
        {
            if (string.IsNullOrEmpty(s)) return AttrId.None;
            switch (s)
            {
                case "AttackRate":       return AttrId.AttackRate;
                case "AttackRateB":      return AttrId.AttackRateB;
                case "HpRate":           return AttrId.HpRate;
                case "AttackSpeedRate":  return AttrId.AttackSpeedRate;
                case "CriticalRate":     return AttrId.CriticalRate;
                case "CriticalDamage":   return AttrId.CriticalDamage;
                case "DamageCount":      return AttrId.DamageCount;
                case "DamageRadiusRate": return AttrId.DamageRadiusRate;
                case "SlowDownRate":     return AttrId.SlowDownRate;
                case "BrickInitCoin":    return AttrId.BrickInitCoin;
                case "BrickWaveCoin":    return AttrId.BrickWaveCoin;
                case "MinerGold":        return AttrId.MinerGold;
                case "NinJiaAttack":     return AttrId.NinJiaAttack;
                default:                 return AttrId.None;
            }
        }

        /// Handles both shapes found in the data:
        ///   "AttackRate,0.03,0.03"  -> base 0.03, perLevel 0.03
        ///   "AttackRateB,+15%"      -> base 0.15, perLevel 0
        ///   "DamageCount,+1"        -> base 1,    perLevel 0
        /// Multiple mods are joined with '|'.
        public static List<AttrMod> Attrs(string s)
        {
            var list = new List<AttrMod>();
            if (string.IsNullOrEmpty(s)) return list;

            foreach (var part in s.Split('|'))
            {
                var f = part.Split(',');
                if (f.Length < 2) continue;

                var id = ToAttrId(f[0]);
                if (id == AttrId.None) continue;

                var mod = new AttrMod { Id = id, Base = Value(f[1]) };
                if (f.Length >= 3) mod.PerLevel = Value(f[2]);
                list.Add(mod);
            }
            return list;
        }

        /// "+15%" -> 0.15, "-50%" -> -0.5, "+1" -> 1, "0.03" -> 0.03
        static float Value(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return 0f;
            var s = raw.Trim();
            bool percent = s.EndsWith("%");
            if (percent) s = s.Substring(0, s.Length - 1);
            if (s.StartsWith("+")) s = s.Substring(1);
            var v = Num.F(s, 0f);
            return percent ? v / 100f : v;
        }

        /// "1,100|2,100" -> [(item 1, x100), (item 2, x100)]
        public static List<RewardEntry> Rewards(string s)
        {
            var list = new List<RewardEntry>();
            if (string.IsNullOrEmpty(s)) return list;

            foreach (var part in s.Split('|'))
            {
                var f = part.Split(',');
                if (f.Length < 2) continue;
                // BoxReward uses a 3-field "type,id,count" form; take the last two
                int idIdx = f.Length >= 3 ? 1 : 0;
                if (!Num.I(f[idIdx], out var id)) continue;
                if (!Num.I(f[idIdx + 1], out var n)) continue;
                list.Add(new RewardEntry { ItemId = id, Count = n });
            }
            return list;
        }

        /// "100|60" -> [100, 60];  "80|15|5" -> [80, 15, 5]
        public static List<int> Ints(string s, char sep = '|')
        {
            var list = new List<int>();
            if (string.IsNullOrEmpty(s)) return list;
            foreach (var p in s.Split(sep))
                if (Num.I(p, out var v)) list.Add(v);
            return list;
        }

        public static List<float> Floats(string s, char sep = '|')
        {
            var list = new List<float>();
            if (string.IsNullOrEmpty(s)) return list;
            foreach (var p in s.Split(sep))
                if (Num.F(p, out var v)) list.Add(v);
            return list;
        }
    }
}
