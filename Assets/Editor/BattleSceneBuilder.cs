using Skyscraper.Battle;
using Skyscraper.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Skyscraper.EditorTools
{
    /// Generates the playable battle scene from code so there is nothing to
    /// wire by hand and the scene can be rebuilt after any script change.
    public static class BattleSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Battle.unity";

        [MenuItem("Skyscraper/Build Battle Scene", priority = 0)]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- camera ---
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.17f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0f, 5.5f, -10f);

            // --- roots ---
            var battle = new GameObject("Battle");
            var brickRoot = new GameObject("Bricks"); brickRoot.transform.SetParent(battle.transform);
            var enemyRoot = new GameObject("Enemies"); enemyRoot.transform.SetParent(battle.transform);
            var fxRoot = new GameObject("Fx"); fxRoot.transform.SetParent(battle.transform);

            // --- ground ---
            // Wide enough that an enemy spawned outside the battlefield bounds
            // still has floor under it: bounds are +-12.5 and the spawn margin
            // adds 4.7 more, so 24 (the old pre-RefScale span) left them in
            // mid-air at the far left and right.
            const float GroundThickness = 0.7f;
            float GroundSpan = Skyscraper.Battle.RefScale.ViewWidth * 1.5f;

            var ground = new GameObject("Ground");
            ground.transform.SetParent(battle.transform);
            ground.transform.position = new Vector3(0f, -GroundThickness * 0.5f, 0f);
            var gCol = ground.AddComponent<BoxCollider2D>();
            gCol.size = new Vector2(GroundSpan, GroundThickness);
            var gSr = new GameObject("Art");
            gSr.transform.SetParent(ground.transform, false);
            gSr.AddComponent<SpriteRenderer>();
            var art = gSr.AddComponent<RuntimeSprite>();
            art.Shape = RuntimeSprite.Kind.Box;
            art.Tint = new Color(0.28f, 0.32f, 0.38f);
            art.SortingOrder = 1;
            art.Size = new Vector2(GroundSpan, GroundThickness);
            art.Apply();

            // --- runtime ---
            var runtime = battle.AddComponent<BattleRuntime>();
            runtime.BrickRoot = brickRoot.transform;
            runtime.EnemyRoot = enemyRoot.transform;
            runtime.FxRoot = fxRoot.transform;
            runtime.Ground = ground.transform;
            runtime.GroundY = 0f;
            runtime.SceneId = 1;
            runtime.Hard = false;

            battle.AddComponent<BrickDropper>();
            battle.AddComponent<MergeSystem>();
            battle.AddComponent<ChallengeModifiers>();
            battle.AddComponent<BattleHud>();
            // Writes <project>/BattleProbe.txt during Play. The editor console
            // is not readable from outside the process, so the game reports on
            // itself instead.
            battle.AddComponent<BattleProbe>();

            EditorSceneManager.MarkSceneDirty(scene);
            // AssetDatabase.CreateFolder, not Directory.CreateDirectory: SaveScene
            // writes through the asset database, which will not see a folder that
            // appeared behind its back until the next import.
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            // Console only, never EditorUtility.DisplayDialog: a modal dialog
            // blocks the editor main thread, which stalls the MCP command loop
            // and drops the caller's connection.
            Debug.Log($"[Skyscraper] battle scene written to {ScenePath}. Press Play.");
        }

        [MenuItem("Skyscraper/Validate Config Tables", priority = 20)]
        public static void Validate()
        {
            try { ConfigDB.LoadAll(); }
            catch (System.Exception e)
            {
                Debug.LogError("[Skyscraper] config load failed: " + e);
                return;
            }

            int waveGroups = 0, badGroups = 0;
            foreach (var w in ConfigDB.Waves)
            {
                foreach (var part in w.Refresh.Split('|'))
                {
                    waveGroups++;
                    if (!RefreshGroup.TryParse(part, out _)) badGroups++;
                }
            }

            int missingEnemy = 0;
            foreach (var w in ConfigDB.Waves)
                foreach (var g in RefreshGroup.ParseAll(w.Refresh))
                    if (ConfigDB.Enemy(g.EnemyId) == null) missingEnemy++;

            int missingLevels = 0;
            foreach (var h in ConfigDB.Heroes)
                if (ConfigDB.HeroLevel(h.ID, 1) == null) missingLevels++;

            var msg =
                $"heroes        {ConfigDB.Heroes.Count}\n" +
                $"hero levels   {ConfigDB.HeroLevels.Count}\n" +
                $"enemies       {ConfigDB.Enemies.Count}\n" +
                $"maps          {ConfigDB.Maps.Count} (hard {ConfigDB.MapsHard.Count})\n" +
                $"wave rows     {ConfigDB.Waves.Count} (hard {ConfigDB.WavesHard.Count})\n" +
                $"cards         {ConfigDB.Cards.Count}\n" +
                $"spawn groups  {waveGroups}, unparsable {badGroups}\n" +
                $"unknown enemy refs {missingEnemy}\n" +
                $"heroes without Lv1 row {missingLevels}";

            Debug.Log("[Skyscraper] config validation\n" + msg);
        }
    }
}
