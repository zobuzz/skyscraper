using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Skyscraper.EditorTools
{
    /// Scripted player builds.
    ///
    /// Exists so an APK is reproducible and can be built headlessly. Building
    /// from the Build Settings dialog by hand works but leaves no record of
    /// which scenes and which settings produced a given file, which matters
    /// here because the Android and Editor code paths differ (see
    /// ConfigDB.StreamingAssetsIsFile).
    public static class BuildPlayerCommands
    {
        const string Apk = "111.apk";

        /// Where BuildAndroid writes its outcome.
        ///
        /// A build takes minutes and blocks the main thread, which drops any
        /// editor-automation connection long before BuildPlayer returns -- the
        /// caller sees a dead socket, not a result. A file survives that, so
        /// the outcome can be read afterwards regardless of how the build was
        /// started.
        public const string ReportFile = "build_report.txt";

        static string ProjectRoot =>
            Directory.GetParent(Application.dataPath).FullName;

        static void Report(string text)
        {
            try { File.WriteAllText(Path.Combine(ProjectRoot, ReportFile), text); }
            catch (Exception e) { Debug.LogWarning($"[Build] report write failed: {e.Message}"); }
        }

        /// Runs the build off the current call stack.
        ///
        /// BuildPlayer refuses to run, or misbehaves, when it is called from
        /// inside another editor callback -- and an automation command handler
        /// is one. Deferring to delayCall means it starts from a clean editor
        /// tick, and lets the caller return immediately instead of holding a
        /// connection open for the whole build.
        public static void QueueBuildAndroid()
        {
            Report("queued\n");
            EditorApplication.delayCall += BuildAndroid;
        }

        [MenuItem("Skyscraper/Build Android APK", priority = 40)]
        public static void BuildAndroid()
        {
            var scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] no enabled scenes in Build Settings");
                Report("FAILED: no enabled scenes in Build Settings\n");
                return;
            }

            // Relative to the project root, matching where the hand-built APK
            // was written.
            string outPath = Path.Combine(ProjectRoot, Apk);

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            Debug.Log($"[Build] Android -> {outPath}\n  scenes: {string.Join(", ", scenes)}");
            Report($"started\n  out: {outPath}\n  scenes: {string.Join(", ", scenes)}\n");

            // Caught rather than left to propagate: BuildPlayer throws
            // BuildFailedException for an unusable environment (no SDK, no NDK,
            // wrong JDK), and an exception thrown here would only reach
            // whatever invoked the build -- which, for automation, is a socket
            // that has already timed out. The message is the whole diagnosis,
            // so it has to reach the file.
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(opts);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Build] threw: {e.GetType().Name}: {e.Message}");
                Report($"THREW {e.GetType().Name}: {e.Message}\n\n{e}\n");
                return;
            }

            var s = report.summary;

            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] ok: {s.totalSize / (1024 * 1024)} MB in {s.totalTime}");
                Report($"SUCCEEDED\n  {s.totalSize / (1024 * 1024)} MB in {s.totalTime}\n" +
                       $"  out: {outPath}\n");
            }
            else
            {
                Debug.LogError($"[Build] {s.result}: {s.totalErrors} errors");
                Report($"{s.result}\n  errors: {s.totalErrors}\n" + StepErrors(report));
            }
        }

        /// The build steps' own messages. summary.totalErrors is only a count;
        /// the reason a build failed is in the per-step log.
        static string StepErrors(BuildReport report)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        sb.Append($"  [{step.name}] {m.content}\n");
            return sb.ToString();
        }

        /// Only the scenes actually ticked in Build Settings, in order. A
        /// disabled scene at index 0 would otherwise become the boot scene and
        /// the player would open on the wrong one.
        static string[] EnabledScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) list.Add(s.path);
            return list.ToArray();
        }
    }
}
