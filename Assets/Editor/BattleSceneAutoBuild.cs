using UnityEditor;
using UnityEngine;

namespace Skyscraper.EditorTools
{
    /// Rebuilds the battle scene once after a recompile that changed its
    /// layout, then clears the flag.
    ///
    /// The scene is generated code-first, so adding a component to the builder
    /// leaves the saved scene stale until someone re-runs the menu item. The
    /// editor's MCP connector does not survive a domain reload, so the rebuild
    /// cannot be triggered from outside -- this makes the editor do it on its
    /// own the next time it regains focus and recompiles.
    ///
    /// Bump SceneRevision whenever BattleSceneBuilder's output changes.
    [InitializeOnLoad]
    static class BattleSceneAutoBuild
    {
        // 2 = added Base pedestal (built at runtime) and BattleProbe.
        const int SceneRevision = 2;
        const string Key = "Skyscraper.SceneRevision";

        static BattleSceneAutoBuild()
        {
            // Deferred: asset importing is still in flight during a static
            // constructor, and NewScene/SaveScene must not run inside it.
            EditorApplication.delayCall += MaybeBuild;
        }

        static void MaybeBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorPrefs.GetInt(Key, 0) >= SceneRevision) return;

            EditorPrefs.SetInt(Key, SceneRevision);
            Debug.Log($"[Skyscraper] scene revision {SceneRevision}: rebuilding Battle.unity");
            BattleSceneBuilder.Build();
        }
    }
}
