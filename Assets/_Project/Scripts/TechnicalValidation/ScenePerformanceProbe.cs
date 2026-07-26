using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PawsAndLoot.Logging;
using UnityEngine;
using UnityEngine.Profiling;

namespace PawsAndLoot.TechnicalValidation
{
    /// <summary>
    /// ART-005. Measures the running scene and writes a JSON report.
    ///
    /// Follows the project's self-reporting probe pattern: activated only by an
    /// explicit command line argument, writes a result file and quits, so a
    /// normal run is unaffected.
    ///
    /// Draw calls are deliberately absent. Unity exposes them through
    /// UnityStats, which is editor only, so a built player cannot report them
    /// honestly; the renderer and material counts here are what a draw call
    /// estimate would be built from.
    /// </summary>
    public sealed class ScenePerformanceProbe : MonoBehaviour
    {
        private const string EnableArgument = "-perfProbe";
        private const string ResultFileName = "art-005-result.json";

        [SerializeField, Min(1f)]
        private float measureSeconds = 8f;

        [SerializeField, Min(0.5f)]
        private float warmupSeconds = 2f;

        private readonly List<float> _frameMilliseconds = new();
        private float _elapsedSeconds;
        private bool _enabled;
        private bool _written;

        /// <summary>
        /// The playtest build starts at Bootstrap, but the scene worth
        /// measuring is the match. When the flag is present the probe advances
        /// past the menu itself so the measurement needs no button press.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoOpenMeasuredScene()
        {
            if (!HasArgument(
                    System.Environment.GetCommandLineArgs(),
                    EnableArgument))
            {
                return;
            }

            string bootstrapName = Core.GameSceneCatalog.GetName(
                Core.GameSceneId.Bootstrap);
            if (UnityEngine.SceneManagement.SceneManager
                    .GetActiveScene().name != bootstrapName)
            {
                return;
            }

            Core.GameSceneLoader.Load(Core.GameSceneId.Game);
        }

        private void Awake()
        {
            _enabled = HasArgument(
                System.Environment.GetCommandLineArgs(),
                EnableArgument);
            if (!_enabled)
            {
                enabled = false;
                return;
            }

            // With VSync on, every frame lands on the refresh interval and an
            // optimisation cannot be observed at all. Uncapping is the only way
            // to see real headroom.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        private void Update()
        {
            if (!_enabled || _written)
            {
                return;
            }

            _elapsedSeconds += Time.unscaledDeltaTime;
            if (_elapsedSeconds < warmupSeconds)
            {
                return;
            }

            _frameMilliseconds.Add(Time.unscaledDeltaTime * 1000f);
            if (_elapsedSeconds < warmupSeconds + measureSeconds)
            {
                return;
            }

            _written = true;
            WriteReport();
            Application.Quit(0);
        }

        private void WriteReport()
        {
            SceneComposition composition = MeasureComposition();
            _frameMilliseconds.Sort();
            float average = 0f;
            foreach (float value in _frameMilliseconds)
            {
                average += value;
            }

            average = _frameMilliseconds.Count > 0
                ? average / _frameMilliseconds.Count
                : 0f;
            float median = _frameMilliseconds.Count > 0
                ? _frameMilliseconds[_frameMilliseconds.Count / 2]
                : 0f;
            float worst1Percent = _frameMilliseconds.Count > 0
                ? _frameMilliseconds[
                    Mathf.Max(
                        0,
                        (int)(_frameMilliseconds.Count * 0.99f) - 1)]
                : 0f;

            var json = new StringBuilder();
            json.AppendLine("{");
            Append(json, "task", "ART-005");
            Append(json, "unityVersion", Application.unityVersion);
            Append(json, "graphicsDevice", SystemInfo.graphicsDeviceName);
            Append(json, "graphicsApi", SystemInfo.graphicsDeviceType.ToString());
            Append(json, "screen", $"{Screen.width}x{Screen.height}");
            AppendNumber(json, "sampleFrames", _frameMilliseconds.Count);
            AppendNumber(json, "averageFrameMs", average);
            AppendNumber(json, "medianFrameMs", median);
            AppendNumber(json, "worst1PercentFrameMs", worst1Percent);
            AppendNumber(
                json,
                "averageFps",
                average > 0.0001f ? 1000f / average : 0f);
            AppendNumber(json, "rendererCount", composition.Renderers);
            AppendNumber(
                json,
                "skinnedMeshCount",
                composition.SkinnedMeshes);
            AppendNumber(json, "maxBonesOnOneMesh", composition.MaxBones);
            AppendNumber(json, "totalBones", composition.TotalBones);
            AppendNumber(
                json,
                "uniqueMaterialCount",
                composition.UniqueMaterials);
            AppendNumber(json, "lightCount", composition.Lights);
            AppendNumber(
                json,
                "textureMemoryMb",
                Texture.currentTextureMemory / (1024f * 1024f));
            AppendNumber(
                json,
                "totalAllocatedMemoryMb",
                Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f));
            json.AppendLine(
                "  \"drawCalls\": \"not measured; UnityStats is editor only\"");
            json.AppendLine("}");

            string directory = Application.persistentDataPath;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, ResultFileName);
            File.WriteAllText(path, json.ToString());
            GameLogger.Info(
                GameLogCategory.Match,
                $"ART-005 report written to '{path}'. "
                + $"avg {average:0.00}ms, renderers {composition.Renderers}.",
                this);
        }

        private struct SceneComposition
        {
            public int Renderers;
            public int SkinnedMeshes;
            public int MaxBones;
            public int TotalBones;
            public int UniqueMaterials;
            public int Lights;
        }

        private static SceneComposition MeasureComposition()
        {
            var composition = new SceneComposition();
            // Compared by reference: GetInstanceID is obsolete in this Unity
            // version and a plain set of the assets themselves is enough.
            var materials = new HashSet<Material>();
            foreach (Renderer renderer in
                Object.FindObjectsByType<Renderer>(
                    FindObjectsSortMode.None))
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                composition.Renderers++;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        materials.Add(material);
                    }
                }
            }

            foreach (SkinnedMeshRenderer skinned in
                Object.FindObjectsByType<SkinnedMeshRenderer>(
                    FindObjectsSortMode.None))
            {
                composition.SkinnedMeshes++;
                int bones = skinned.bones != null ? skinned.bones.Length : 0;
                composition.TotalBones += bones;
                composition.MaxBones = Mathf.Max(
                    composition.MaxBones,
                    bones);
            }

            composition.Lights = Object.FindObjectsByType<Light>(
                FindObjectsSortMode.None).Length;
            composition.UniqueMaterials = materials.Count;
            return composition;
        }

        private static bool HasArgument(
            IReadOnlyList<string> arguments,
            string flag)
        {
            if (arguments == null)
            {
                return false;
            }

            for (int index = 0; index < arguments.Count; index++)
            {
                if (string.Equals(
                        arguments[index],
                        flag,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Append(
            StringBuilder json,
            string key,
            string value)
        {
            json.AppendLine($"  \"{key}\": \"{value}\",");
        }

        private static void AppendNumber(
            StringBuilder json,
            string key,
            float value)
        {
            json.AppendLine(
                $"  \"{key}\": "
                + value.ToString("0.###", CultureInfo.InvariantCulture)
                + ",");
        }
    }
}
