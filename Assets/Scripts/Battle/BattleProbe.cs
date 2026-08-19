using System.Collections.Generic;
using System.Text;
using Skyscraper.Config;
using UnityEngine;

namespace Skyscraper.Battle
{
    /// Writes a plain-text report of what the battle actually did at runtime.
    ///
    /// This exists because the editor's console is not always reachable from
    /// outside the process: the MCP connector drops on domain reload, and the
    /// shared Editor.log belongs to whichever Unity instance grabbed it first.
    /// A file the game writes itself is the one channel that always works.
    ///
    /// Output: &lt;project&gt;/BattleProbe.txt, rewritten from scratch each Play.
    public class BattleProbe : MonoBehaviour
    {
        [Tooltip("Seconds between snapshots appended to the report.")]
        public float SampleInterval = 1f;
        [Tooltip("Stop sampling after this many seconds; 0 = never stop.")]
        public float Duration = 60f;

        public string OutputPath => System.IO.Path.Combine(
            System.IO.Directory.GetParent(Application.dataPath).FullName, "BattleProbe.txt");

        BattleRuntime _runtime;
        BrickDropper _dropper;
        BattleContext _ctx;

        readonly StringBuilder _sb = new StringBuilder();
        readonly List<string> _draws = new List<string>();
        readonly Dictionary<string, int> _drawCount = new Dictionary<string, int>();

        float _next, _elapsed;
        readonly List<string> _slotSeen = new List<string>();
        bool _headerDone, _finished;

        void RecordHand()
        {
            if (_dropper == null) return;
            var hand = _dropper.Hand;
            while (_slotSeen.Count < hand.Count) _slotSeen.Add(null);

            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                if (card == null || card.Row == null) continue;
                string sig = $"{card.Row.ID}:{card.Shape}";
                if (_slotSeen[i] == sig) continue;
                _slotSeen[i] = sig;

                var h = card.Row;
                var key = $"{h.ID} {h.Name}";
                _drawCount.TryGetValue(key, out var n);
                _drawCount[key] = n + 1;
                if (_draws.Count < 200)
                    _draws.Add($"[{_elapsed,6:0.0}s] slot{i} {h.ID} {h.Name} Q{h.Quality} " +
                               $"footprint={card.Shape} cost={h.Cost} " +
                               $"merge={h.MergeSkin} skill={h.SkillType} interval={h.Interval}");
            }
        }

        void Start()
        {
            _runtime = GetComponent<BattleRuntime>();
            _dropper = GetComponent<BrickDropper>();
            if (_runtime == null) { enabled = false; return; }

            _runtime.OnLog += OnBattleLog;
            _ctx = _runtime.Ctx;

            // Start() ordering between components is undefined, so the header
            // is written from Update once the runtime has a map.
        }

        void OnDestroy()
        {
            if (_runtime != null) _runtime.OnLog -= OnBattleLog;
            Finish("OnDestroy");
        }

        void OnApplicationQuit() => Finish("quit");

        void OnBattleLog(string msg) => _sb.AppendLine($"[{_elapsed,6:0.0}s] LOG  {msg}");

        void Update()
        {
            if (_finished) return;
            _elapsed += Time.deltaTime;

            if (!_headerDone)
            {
                if (_ctx == null || _ctx.Map == null) return;
                WriteHeader();
                _headerDone = true;
                _next = 0f;
            }

            // Record every card the hand offers -- this is the answer to "which
            // bricks does the game actually generate". Tracked per slot, since
            // the hand is three deep and only the played slot is refilled.
            RecordHand();

            if (_elapsed >= _next)
            {
                _next = _elapsed + Mathf.Max(0.1f, SampleInterval);
                Snapshot();
                Flush();
            }

            if (Duration > 0f && _elapsed >= Duration) Finish("duration reached");
        }

        void WriteHeader()
        {
            var m = _ctx.Map;
            _sb.AppendLine("=== Skyscraper battle probe ===");
            _sb.AppendLine($"unity        {Application.unityVersion}");
            _sb.AppendLine($"scene        {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _sb.AppendLine();
            _sb.AppendLine($"map          {m.ID} {m.Title} {m.ChapterTitle}");
            _sb.AppendLine($"waves        {_ctx.TotalWaves}   hard={_ctx.Hard}");
            _sb.AppendLine($"BattleGold   {m.BattleGold}  -> start {_ctx.Gold}");
            _sb.AppendLine($"kill bounty  normal {_ctx.KillReward(false)}   boss {_ctx.KillReward(true)}");
            _sb.AppendLine($"bounds       [{_ctx.LeftBound}, {_ctx.RightBound}]");
            _sb.AppendLine($"desk         {m.Desk}");
            _sb.AppendLine($"base         {(_runtime.Base != null ? _runtime.Base.ToString() : "MISSING")}");
            _sb.AppendLine($"challenges   {m.ChallengeType}");
            _sb.AppendLine();

            if (_dropper != null)
            {
                _sb.AppendLine($"--- draw pool (PlayerLevel={_dropper.PlayerLevel}) : {_dropper.Pool.Count} heroes ---");
                _sb.AppendLine("  ID   Name        Q  UnLock  Cost  Weight  Merge Skill Interval  Footprints");
                foreach (var h in _dropper.Pool)
                {
                    var names = new System.Text.StringBuilder();
                    foreach (var s in BrickShape.SetFor(h))
                    {
                        if (names.Length > 0) names.Append('/');
                        names.Append(s.Name);
                    }
                    _sb.AppendLine($"  {h.ID,-4} {h.Name,-10} {h.Quality}  {h.UnLock,-6} {h.Cost,-5} " +
                                   $"{h.RandomTimes,-6}  {h.MergeSkin,-5} {h.SkillType,-5} " +
                                   $"{h.Interval,-9:0.00} {names}");
                }

                _sb.AppendLine();
                _sb.AppendLine("--- excluded from pool ---");
                foreach (var h in ConfigDB.Heroes)
                    if (!BrickDropper.IsUnlocked(h, _dropper.PlayerLevel))
                        _sb.AppendLine($"  {h.ID} {h.Name} Q{h.Quality} UnLock={h.UnLock}" +
                                       (h.UnLock >= BrickDropper.NeverUnlocks ? "  (never)" : "  (level-gated)"));

                _sb.AppendLine();
                _sb.AppendLine($"tier weights Global.HeroQualityWeight = " +
                               $"{(ConfigDB.Global != null ? ConfigDB.Global.HeroQualityWeight : "(no Global row)")}");
                _sb.AppendLine($"hand size    {_dropper.Hand.Count}   reroll cost {_dropper.RerollCost} " +
                               $"(Global.ChallRefreshCost)");
            }

            _sb.AppendLine();
            _sb.AppendLine("--- height bands (authored: no table supplies these) ---");
            foreach (var b in HeightBonus.Bands)
                _sb.AppendLine($"  >= {b.Label,-6} {b.BonusLabel}");
            _sb.AppendLine($"scale        1 cell = {HeightBonus.MetresPerCell}M " +
                           $"({HeightBonus.MetresPerUnit:0.##} M per world unit)");
            _sb.AppendLine($"white line   tower top + {_runtime.DropLineClearance:0.##} " +
                           $"-> starts at y={_runtime.DropLineY:0.##}");
            _sb.AppendLine($"base hp      {BaseHealth.DefaultMaxHp:0} start (authored: no table " +
                           $"carries it) + the standing bricks' BrickHeroLevel.Hp");
            _sb.AppendLine();
            _sb.AppendLine("--- timeline ---");
        }

        void Snapshot()
        {
            int settled = 0;
            foreach (var b in _runtime.Bricks)
                if (b != null && b.Settled) settled++;

            var hp = _runtime.BaseHp;
            string hpText = hp != null
                ? $"{hp.Hp:0}/{hp.MaxHp:0}(+{hp.BrickBonus:0} heal {hp.Healed:0})"
                : "n/a";

            _sb.AppendLine($"[{_elapsed,6:0.0}s] gold={_ctx.Gold,-5} " +
                           $"kills={_ctx.Kills,-4} goldFromKills={_ctx.GoldFromKills,-5} " +
                           $"goldFromWaves={_ctx.GoldFromWaves,-5} " +
                           $"bricks={_runtime.Bricks.Count,-3}(settled {settled}) " +
                           $"grounded={_runtime.BricksLostToGround,-3} " +
                           $"enemies={_runtime.AliveEnemies,-3} " +
                           $"baseHp={hpText,-26} " +
                           $"height={_runtime.TowerMetres,-5:0.0}M atkMul={_runtime.HeightAttackMul,-4:0.00} " +
                           $"line={_runtime.DropLineY,-5:0.00} " +
                           $"wave={_ctx.WaveIndex + 1}/{_ctx.TotalWaves} phase={_ctx.Phase}");
        }

        void Finish(string why)
        {
            if (_finished || !_headerDone) return;
            _finished = true;

            _sb.AppendLine();
            _sb.AppendLine($"--- finished ({why}) at {_elapsed:0.0}s ---");
            _sb.AppendLine();
            _sb.AppendLine($"--- draws offered ({_draws.Count} recorded) ---");
            foreach (var d in _draws) _sb.AppendLine(d);

            _sb.AppendLine();
            _sb.AppendLine("--- draw frequency ---");
            foreach (var kv in _drawCount) _sb.AppendLine($"  {kv.Key,-16} {kv.Value}");

            Flush();
        }

        void Flush()
        {
            try { System.IO.File.WriteAllText(OutputPath, _sb.ToString(), Encoding.UTF8); }
            catch (System.Exception e) { Debug.LogWarning("[Probe] write failed: " + e.Message); }
        }
    }
}
