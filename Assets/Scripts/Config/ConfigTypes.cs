using System;
using System.Collections.Generic;

namespace Skyscraper.Config
{
    // Field names must match the JSON exactly -- JsonUtility is case-sensitive
    // and silently drops anything it cannot map. Tables come from the extracted
    // StreamingAssets/GameConfig/*.json.

    [Serializable]
    public class BrickHeroRow
    {
        public int ID;
        public string Name;
        public string Desc;
        public int UnLock;        // player level this hero unlocks at; 9999 = never
        public string UnLockTips;
        public int Quality;       // 1 = common, 2 = rare, 3 = epic
        public string Shape;      // Cube_1 .. Cube_16, one shape per hero
        public int RandomTimes;   // draw weight inside its quality tier
        public string ModelRes;
        public int MergeSkin;     // heroes sharing this value merge together
        public int SkillType;     // 0 = wall (no attack), 1..10 = firing patterns
        public float BurstTime;   // wind-up before the shot lands
        public float Interval;    // seconds between shots (0 = never fires)
        public float DmgRadius;   // splash radius, 0 = single target
        public int DmgCount;      // extra hit count for multi-hit skills
        public int Cost;          // gold to place
        public string AttackSound;
        public string HitSound;
        public float CriticalRate;
        public float CriticalDamage;
    }

    [Serializable]
    public class BrickHeroLevelRow
    {
        public int ID;
        public int HeroId;
        public int Lv;
        public int Gold;      // upgrade cost
        public int Clip;      // fragment cost
        public float Attack;
        public float Hp;
        public string Attrs;  // e.g. "AttackRateB,+15%" -- unlocked at this level
        public string UnLockText;
    }

    [Serializable]
    public class BrickEnemyRow
    {
        public int ID;
        public string Model;
        public float Scale;
        public bool IsBoss;
        public float MoveY;   // vertical drift; >0 flies up, <0 sinks
        public float Interval;
        public float Burst;
        public int IsMove;    // 0 = stationary turret type
    }

    [Serializable]
    public class BrickMonsterRow
    {
        public int ID;
        public int Scene;      // -> BrickMap.ID
        public int Wave;       // 1..30
        public int WaveType;   // 1 normal, 2 elite, 3 boss
        public string Refresh; // groups joined by '|', see RefreshGroup
    }

    [Serializable]
    public class BrickMapRow
    {
        public int ID;
        public string Title;         // "1-1"
        public string ChapterTitle;  // "幽影森林"
        public int MapBg;
        public string MapViewBg;
        public int MapColor;
        public int MapViewStage;
        public string MapAmbience;
        public int Difficulty;
        public string ChallengeType; // -> ChallengeAttr ids, '|' separated
        public int UnlockLevel;
        public string BattleGold;    // "initial|perWave"
        public string Reward;
        public string BoxReward;
        public string BoxWave;       // "10|20|30" -- waves that drop a chest
        public string Box1;
        public string Box2;
        public string Box3;
        public int Desk;             // pedestal art variant; observed 1..25 and -1
        public float MonsterLeft;    // battlefield bounds, shrinks in later chapters
        public float MonsterRight;
    }

    [Serializable]
    public class BrickCardRow
    {
        public int ID;
        public string Name;
        public string Desc;
        public int Quality;
        public int Weight;
        public string Corner;
        public int AdFragNum;
        public string Attr;  // "AttackRate,0.03,0.03" = name,base,perLevel
    }

    [Serializable]
    public class BrickCardLevelRow
    {
        public int ID;
        public int Cost0, Cost1, Cost2, Cost3;
    }

    [Serializable]
    public class ChallengeAttrRow
    {
        public int ID;
        public string Name;
        public string Icon;
        public string Desc;
    }

    [Serializable]
    public class ItemRow
    {
        public int ID;
        public int Type;
        public string Name;
        public int Quality;
        public string Icon;
        public int MaxCount;
        public string Description;   // the column is Description, not Desc
    }

    [Serializable]
    public class StarBonusRow
    {
        public int ID;
        public string Reward;
        public int StarNeed;
    }

    [Serializable]
    public class GlobalRow
    {
        public int ID;
        public int PowerRefreshTIme;   // note: typo is in the source data
        public int MaxPower;
        public int PowerCost;
        public int MaxChapter;
        public int RogueAllGetTimes;
        public int RogueRefreshTimes;
        public string HeroQualityWeight;   // "80|15|5"
        public string CollectQualityWeight;
        public string TowerQualityAdd;
        public string InitialItem;
        public int ChallAntiAirHeight;     // 15 -- units above this are destroyed
        public int ChallRefreshCost;
        public int ChallSwampHP;
        public int ChallGiantCount;
        public float ChallGiantScale;
        public float ChallGravityScale;    // 4
        public float ChallWindCD;          // 60
        // Both Gaze fields are per-stage lists, not scalars: "20|20|20" and
        // "10|20|30" -- the gaze rises through three height bands. Declaring
        // this float made JsonUtility throw and took the whole Global row down.
        public string ChallGazeStepCD;
        public string ChallGazeHeight;
        public int MinerGoldCap;
        public string MinimalistExcluded;
        public float WindForce;            // 200
    }

    /// One spawn group inside BrickMonster.Refresh.
    /// Encoding: startTime, spawnInterval, enemyId, count, hp, attack
    /// Verified: field0 is monotonically increasing across all 6962 rows, so it
    /// is a timeline offset, not a count. field3 is always 1 on boss waves.
    public struct RefreshGroup
    {
        public float StartTime;
        public float SpawnInterval;
        public int EnemyId;
        public int Count;
        public double Hp;      // reaches 5.4e6 in late chapters
        public float Attack;

        public static bool TryParse(string s, out RefreshGroup g)
        {
            g = default;
            if (string.IsNullOrEmpty(s)) return false;
            var f = s.Split(',');
            if (f.Length < 6) return false;
            return Num.F(f[0], out g.StartTime)
                 & Num.F(f[1], out g.SpawnInterval)
                 & Num.I(f[2], out g.EnemyId)
                 & Num.I(f[3], out g.Count)
                 & Num.D(f[4], out g.Hp)
                 & Num.F(f[5], out g.Attack);
        }

        public static List<RefreshGroup> ParseAll(string refresh)
        {
            var list = new List<RefreshGroup>();
            if (string.IsNullOrEmpty(refresh)) return list;
            foreach (var part in refresh.Split('|'))
                if (TryParse(part, out var g)) list.Add(g);
            return list;
        }
    }
}
