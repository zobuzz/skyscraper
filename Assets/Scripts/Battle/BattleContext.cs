using System;
using System.Collections.Generic;
using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    public enum BattlePhase { Idle, Running, Won, Lost }

    /// Per-run state: gold economy, wave counter, active modifiers, bounds.
    /// Pure data + events, no scene dependencies, so it can be unit tested.
    public class BattleContext
    {
        public BrickMapRow Map { get; private set; }
        public bool Hard { get; private set; }
        public AttrSet Attrs { get; } = new AttrSet();

        public BattlePhase Phase { get; set; } = BattlePhase.Idle;
        public int Gold { get; private set; }
        public int WaveIndex { get; set; }          // 0-based, current wave
        public int TotalWaves { get; private set; }

        public float LeftBound { get; private set; }
        public float RightBound { get; private set; }

        /// Waves that drop a chest, from BrickMap.BoxWave ("10|20|30").
        public HashSet<int> ChestWaves { get; } = new HashSet<int>();
        public readonly List<int> ActiveChallenges = new List<int>();

        int _initialGold;
        int _perWaveGold;

        public event Action<int> GoldChanged;
        public event Action<int> WaveStarted;
        public event Action<BattlePhase> PhaseChanged;

        public void Setup(BrickMapRow map, bool hard, AttrSet metaAttrs)
        {
            Map = map;
            Hard = hard;
            Attrs.Clear();
            if (metaAttrs != null) Attrs.Add(metaAttrs);

            var gold = Parse.Ints(map.BattleGold);           // "100|60"
            _initialGold = gold.Count > 0 ? gold[0] : 100;
            _perWaveGold = gold.Count > 1 ? gold[1] : 50;

            // MonsterLeft/Right are in the source project's units (chapter 1 is
            // +-4), so they go through RefScale.FromSource like every other
            // length read out of these tables.
            LeftBound = map.MonsterLeft * RefScale.FromSource;
            RightBound = map.MonsterRight * RefScale.FromSource;

            ChestWaves.Clear();
            foreach (var w in Parse.Ints(map.BoxWave)) ChestWaves.Add(w);

            ActiveChallenges.Clear();
            foreach (var c in Parse.Ints(map.ChallengeType)) ActiveChallenges.Add(c);

            TotalWaves = ConfigDB.ScenWaves(map.ID, hard).Count;
            WaveIndex = 0;

            // BrickInitCoin cards add on top of the map's starting purse.
            Gold = _initialGold + Attrs.InitCoin;
            Kills = 0;
            GoldFromKills = 0;
            GoldFromWaves = 0;
            Phase = BattlePhase.Idle;
        }

        public bool HasChallenge(int id) => ActiveChallenges.Contains(id);

        public bool TrySpend(int amount)
        {
            if (amount > Gold) return false;
            Gold -= amount;
            GoldChanged?.Invoke(Gold);
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount == 0) return;
            Gold = Mathf.Max(0, Gold + amount);
            GoldChanged?.Invoke(Gold);
        }

        /// Called when a wave is cleared: pays the per-wave purse plus any
        /// BrickWaveCoin bonus from equipped cards.
        public void PayWaveReward()
        {
            int amount = _perWaveGold + Attrs.WaveCoin;
            GoldFromWaves += amount;
            AddGold(amount);
        }

        // --- kill bounty ---------------------------------------------------
        // No table stores a per-kill payout. BrickEnemy carries only ID/Model/
        // Scale/IsBoss/MoveY/Interval/Burst/IsMove, and no other config file
        // has a gold column keyed by enemy id -- BattleGold (map),
        // BrickHeroLevel.Gold (an upgrade cost) and Global.MinerGoldCap are
        // the only gold fields in the whole set. So the bounty is derived
        // rather than read, and it is derived from the map's own per-wave
        // purse so it stays in proportion across all 233 maps instead of being
        // a constant that is generous in chapter 1 and worthless in chapter 12.
        public const float KillGoldShare = 0.06f;   // ~4 gold on a 60/wave map
        public const int BossKillMultiplier = 8;

        public int Kills { get; private set; }
        public int GoldFromKills { get; private set; }
        public int GoldFromWaves { get; private set; }

        public int KillReward(bool isBoss)
        {
            int baseGold = Mathf.Max(1, Mathf.RoundToInt(_perWaveGold * KillGoldShare));
            return isBoss ? baseGold * BossKillMultiplier : baseGold;
        }

        public void PayKillReward(bool isBoss)
        {
            int amount = KillReward(isBoss);
            Kills++;
            GoldFromKills += amount;
            AddGold(amount);
        }

        public void NotifyWaveStarted(int oneBasedWave) => WaveStarted?.Invoke(oneBasedWave);

        public void SetPhase(BattlePhase p)
        {
            if (Phase == p) return;
            Phase = p;
            PhaseChanged?.Invoke(p);
        }
    }
}
