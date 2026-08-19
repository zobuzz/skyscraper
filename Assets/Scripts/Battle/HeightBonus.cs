using System.Collections.Generic;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// One rung of the height ruler: reach its threshold and the whole tower
    /// gets the attack bonus.
    public struct HeightBand
    {
        public float Metres;        // threshold you must reach to be in this band
        public float AttackAdd;     // additive attack rate, 0.2 = +20%
        public Color Tint;          // ruler segment colour
        public HeightBand(float m, float add, Color t) { Metres = m; AttackAdd = add; Tint = t; }
        public string Label => Metres <= 0f ? "0" : $"{Metres:0}M";
        public string BonusLabel => AttackAdd <= 0f ? "-" : $"攻击+{AttackAdd * 100f:0}%";
    }

    /// Height -> attack bonus, the left-edge ruler from the reference.
    ///
    /// IMPORTANT, and stated rather than hidden: the extracted tables do NOT
    /// contain this. All 28 files were swept -- the only Height columns anywhere
    /// are Global.ChallAntiAirHeight (15) and Global.ChallGazeHeight
    /// ("10|20|30"), both of which belong to challenge modifiers, not to a
    /// tower buff; no column named after a bonus/attack rate is keyed by height,
    /// and no table holds a "10|20|50|100" style band list. So the thresholds
    /// and colours below are read off the reference screenshot (0/10/20/50/100
    /// metres, green / cyan / magenta / orange) and the one bonus the screenshot
    /// actually prints -- 攻击+20% on the 10M rung -- is exact. The three higher
    /// percentages are this project's own progression, kept here in one place so
    /// they can be replaced the moment a real table shows up.
    public static class HeightBonus
    {
        /// One stacked cell reads as one metre; that is the only mapping that
        /// makes the ruler mean anything the player can see themselves build.
        public const float MetresPerCell = 1f;
        public static float MetresPerUnit => MetresPerCell / BrickShape.CellSize;

        static readonly HeightBand[] _bands =
        {
            new HeightBand(0f,   0.00f, new Color(0.36f, 0.80f, 0.36f)),   // green
            new HeightBand(10f,  0.20f, new Color(0.30f, 0.82f, 0.85f)),   // cyan   (from art)
            new HeightBand(20f,  0.50f, new Color(0.85f, 0.35f, 0.78f)),   // magenta
            new HeightBand(50f,  1.00f, new Color(0.95f, 0.60f, 0.20f)),   // orange
            new HeightBand(100f, 2.00f, new Color(0.95f, 0.28f, 0.28f)),   // past the top
        };

        public static IReadOnlyList<HeightBand> Bands => _bands;

        /// Top of the drawn ruler. Bands above this still pay out; the marker
        /// just pins to the top.
        public static float RulerTopMetres => _bands[_bands.Length - 1].Metres;

        public static float Metres(float worldY, float groundY) =>
            Mathf.Max(0f, worldY - groundY) * MetresPerUnit;

        public static int BandIndex(float metres)
        {
            int idx = 0;
            for (int i = 0; i < _bands.Length; i++)
                if (metres >= _bands[i].Metres) idx = i;
            return idx;
        }

        public static HeightBand BandAt(float metres) => _bands[BandIndex(metres)];

        public static float AttackAdd(float metres) => _bands[BandIndex(metres)].AttackAdd;

        /// The multiplier BrickUnit folds into its damage. Applied at fire time
        /// rather than in RecalcStats so it tracks the tower as it grows and
        /// shrinks, without recomputing every brick's stat block each frame.
        public static float AttackMul(float metres) => 1f + AttackAdd(metres);

        /// Fraction 0..1 up the drawn ruler. Bands get one equal segment each
        /// -- a linear 0..100 axis would squash the first two rungs, which are
        /// the ones the player spends most of a run inside.
        public static float RulerFraction(float metres)
        {
            int last = _bands.Length - 1;
            if (metres >= _bands[last].Metres) return 1f;
            int i = BandIndex(metres);
            float lo = _bands[i].Metres, hi = _bands[i + 1].Metres;
            float within = hi > lo ? Mathf.Clamp01((metres - lo) / (hi - lo)) : 0f;
            return (i + within) / last;
        }
    }
}
