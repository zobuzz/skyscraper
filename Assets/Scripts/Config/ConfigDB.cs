using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Skyscraper.Config
{
    /// Loads the extracted config tables from StreamingAssets/GameConfig.
    ///
    /// The tables are top-level JSON arrays, which JsonUtility refuses to
    /// deserialize directly. Wrapping them in {"items":[...]} sidesteps that
    /// without pulling in a JSON package, so the project stays dependency-free.
    public static class ConfigDB
    {
        public const string Folder = "GameConfig";

        [Serializable] class Wrapper<T> { public List<T> items; }

        public static bool Loaded { get; private set; }

        public static List<BrickHeroRow>      Heroes      { get; private set; }
        public static List<BrickHeroLevelRow> HeroLevels  { get; private set; }
        public static List<BrickEnemyRow>     Enemies     { get; private set; }
        public static List<BrickMonsterRow>   Waves       { get; private set; }
        public static List<BrickMapRow>       Maps        { get; private set; }
        public static List<BrickMapRow>       MapsHard    { get; private set; }
        public static List<BrickMonsterRow>   WavesHard   { get; private set; }
        public static List<BrickCardRow>      Cards       { get; private set; }
        public static List<ChallengeAttrRow>  Challenges  { get; private set; }
        public static List<ItemRow>           Items       { get; private set; }
        public static List<StarBonusRow>      StarBonus   { get; private set; }
        public static GlobalRow               Global      { get; private set; }

        // --- indexes -------------------------------------------------------
        public static Dictionary<int, BrickHeroRow>  HeroById   { get; private set; }
        public static Dictionary<int, BrickEnemyRow> EnemyById  { get; private set; }
        public static Dictionary<int, BrickMapRow>   MapById    { get; private set; }
        public static Dictionary<int, ChallengeAttrRow> ChallengeById { get; private set; }
        /// (heroId, level) -> row
        public static Dictionary<long, BrickHeroLevelRow> HeroLevelByKey { get; private set; }
        /// scene -> waves ordered by Wave
        public static Dictionary<int, List<BrickMonsterRow>> WavesByScene { get; private set; }
        public static Dictionary<int, List<BrickMonsterRow>> WavesHardByScene { get; private set; }

        public static long LevelKey(int heroId, int lv) => ((long)heroId << 16) | (uint)lv;

        static string PathFor(string table) =>
            Path.Combine(Application.streamingAssetsPath, Folder, table + ".json");

        /// True where StreamingAssets is a real directory that File can open.
        ///
        /// On Android it is not: the files stay compressed inside the APK and
        /// Application.streamingAssetsPath is a "jar:file:///...apk!/assets"
        /// URL, so File.ReadAllText fails with "Could not find a part of the
        /// path". WebGL is a URL for the same reason. Both must go through
        /// UnityWebRequest instead.
        ///
        /// Detected from the path rather than with #if UNITY_ANDROID so that
        /// the decision is made on the property that actually matters. A URL
        /// scheme is exactly what File cannot open.
        public static bool StreamingAssetsIsFile =>
            !Application.streamingAssetsPath.Contains("://");

        // ------------------------------------------------------------------
        /// Synchronous load. Valid in the Editor and on standalone players,
        /// where StreamingAssets is a real directory on disk.
        ///
        /// Check StreamingAssetsIsFile before calling this, or prefer
        /// LoadAny, which picks for you.
        public static void LoadAll()
        {
            Build(t => File.ReadAllText(PathFor(t)));
        }

        /// The load to call when you do not want to care which platform you are
        /// on: reads directly where that works and falls back to the request
        /// path where it does not. Always drive it as a coroutine.
        ///
        /// Exists because callers kept reaching for LoadAll -- it is the
        /// convenient one and it works in the Editor, so a build that only ever
        /// ran in the Editor looks correct right up until the APK boots to an
        /// empty screen.
        public static IEnumerator LoadAny(Action<string> onError = null)
        {
            if (StreamingAssetsIsFile)
            {
                try { LoadAll(); }
                catch (Exception e) { onError?.Invoke(e.Message); }
                yield break;
            }
            yield return LoadAllAsync(onError);
        }

        /// Coroutine load for Android / WebGL, where StreamingAssets lives
        /// inside the package and must go through UnityWebRequest.
        public static IEnumerator LoadAllAsync(Action<string> onError = null)
        {
            var texts = new Dictionary<string, string>();
            foreach (var t in RequiredTables)
            {
                var url = PathFor(t);
                if (!url.Contains("://")) url = "file://" + url;

                using (var req = UnityWebRequest.Get(url))
                {
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke($"{t}: {req.error}");
                        yield break;
                    }
                    texts[t] = req.downloadHandler.text;
                }
            }
            Build(t => texts[t]);
        }

        static readonly string[] RequiredTables =
        {
            "BrickHero", "BrickHeroLevel", "BrickEnemy", "BrickMonster", "BrickMonsterB",
            "BrickMap", "BrickMapB", "BrickCard", "ChallengeAttr", "Item", "StarBonus", "Global",
        };

        static void Build(Func<string, string> read)
        {
            Heroes     = Rows<BrickHeroRow>(read("BrickHero"));
            HeroLevels = Rows<BrickHeroLevelRow>(read("BrickHeroLevel"));
            Enemies    = Rows<BrickEnemyRow>(read("BrickEnemy"));
            Waves      = Rows<BrickMonsterRow>(read("BrickMonster"));
            WavesHard  = Rows<BrickMonsterRow>(read("BrickMonsterB"));
            Maps       = Rows<BrickMapRow>(read("BrickMap"));
            MapsHard   = Rows<BrickMapRow>(read("BrickMapB"));
            Cards      = Rows<BrickCardRow>(read("BrickCard"));
            Challenges = Rows<ChallengeAttrRow>(read("ChallengeAttr"));
            Items      = Rows<ItemRow>(read("Item"));
            StarBonus  = Rows<StarBonusRow>(read("StarBonus"));

            var globals = Rows<GlobalRow>(read("Global"));
            Global = globals.Count > 0 ? globals[0] : new GlobalRow();

            HeroById      = Index(Heroes,     r => r.ID);
            EnemyById     = Index(Enemies,    r => r.ID);
            MapById       = Index(Maps,       r => r.ID);
            ChallengeById = Index(Challenges, r => r.ID);

            HeroLevelByKey = new Dictionary<long, BrickHeroLevelRow>(HeroLevels.Count);
            foreach (var r in HeroLevels) HeroLevelByKey[LevelKey(r.HeroId, r.Lv)] = r;

            WavesByScene     = GroupWaves(Waves);
            WavesHardByScene = GroupWaves(WavesHard);

            Loaded = true;
            Debug.Log($"[ConfigDB] heroes={Heroes.Count} levels={HeroLevels.Count} " +
                      $"enemies={Enemies.Count} maps={Maps.Count} waveRows={Waves.Count} cards={Cards.Count}");
        }

        static Dictionary<int, List<BrickMonsterRow>> GroupWaves(List<BrickMonsterRow> src)
        {
            var d = new Dictionary<int, List<BrickMonsterRow>>();
            foreach (var r in src)
            {
                if (!d.TryGetValue(r.Scene, out var list))
                    d[r.Scene] = list = new List<BrickMonsterRow>();
                list.Add(r);
            }
            foreach (var list in d.Values) list.Sort((a, b) => a.Wave.CompareTo(b.Wave));
            return d;
        }

        static List<T> Rows<T>(string json)
        {
            var w = JsonUtility.FromJson<Wrapper<T>>("{\"items\":" + json + "}");
            return w?.items ?? new List<T>();
        }

        static Dictionary<int, T> Index<T>(List<T> rows, Func<T, int> key)
        {
            var d = new Dictionary<int, T>(rows.Count);
            foreach (var r in rows) d[key(r)] = r;
            return d;
        }

        // --- convenience ---------------------------------------------------
        public static BrickHeroRow Hero(int id) => HeroById != null && HeroById.TryGetValue(id, out var r) ? r : null;
        public static BrickEnemyRow Enemy(int id) => EnemyById != null && EnemyById.TryGetValue(id, out var r) ? r : null;
        public static BrickMapRow Map(int id) => MapById != null && MapById.TryGetValue(id, out var r) ? r : null;

        public static BrickHeroLevelRow HeroLevel(int heroId, int lv) =>
            HeroLevelByKey != null && HeroLevelByKey.TryGetValue(LevelKey(heroId, lv), out var r) ? r : null;

        /// Highest configured level for a hero (30 in the shipped data).
        public static int MaxHeroLevel(int heroId)
        {
            int max = 0;
            foreach (var r in HeroLevels) if (r.HeroId == heroId && r.Lv > max) max = r.Lv;
            return max;
        }

        public static List<BrickMonsterRow> ScenWaves(int scene, bool hard = false)
        {
            var src = hard ? WavesHardByScene : WavesByScene;
            return src != null && src.TryGetValue(scene, out var l) ? l : new List<BrickMonsterRow>();
        }
    }
}
