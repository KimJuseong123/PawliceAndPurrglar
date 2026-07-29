using System.Collections.Generic;
using PawsAndLoot.Animation;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// ART-012. Reduces the draw cost of the assembled Game scene.
    ///
    /// Runs at the end of the scene rebuild rather than by hand, so the
    /// optimisation can never drift out of sync with the generated content.
    ///
    /// Three passes, each safe for gameplay:
    ///   - identical materials are collapsed onto one shared asset
    ///   - never-moving renderers are flagged static so Unity can batch them
    ///   - shadow casting is dropped from small props that cannot be seen to
    ///     cast a meaningful shadow from a fixed top-down camera
    ///
    /// Colliders, transforms and component references are never touched.
    /// </summary>
    internal static class SceneOptimizationPass
    {
        /// <summary>
        /// Roots whose children move at runtime and therefore must not be
        /// marked static.
        /// </summary>
        private static readonly string[] DynamicRootNames =
        {
            "Police Player",
            "Thief Player",
            "Companions",
            "Map Traversal Probe",
            "PLAYER-004 Interaction Targets"
        };

        public readonly struct Report
        {
            public Report(
                int renderersBefore,
                int materialsBefore,
                int materialsAfter,
                int markedStatic,
                int shadowsDisabled)
            {
                RenderersBefore = renderersBefore;
                MaterialsBefore = materialsBefore;
                MaterialsAfter = materialsAfter;
                MarkedStatic = markedStatic;
                ShadowsDisabled = shadowsDisabled;
            }

            public int RenderersBefore { get; }
            public int MaterialsBefore { get; }
            public int MaterialsAfter { get; }
            public int MarkedStatic { get; }
            public int ShadowsDisabled { get; }
        }

        public static Report Run(GameObject villageRoot)
        {
            var renderers = new List<Renderer>(
                villageRoot.GetComponentsInChildren<Renderer>(true));
            var before = new HashSet<Material>();
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        before.Add(material);
                    }
                }
            }

            int consolidated = ConsolidateMaterials(renderers);
            int markedStatic = MarkStatic(renderers);
            int shadowsDisabled = TrimShadows(renderers);

            var after = new HashSet<Material>();
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        after.Add(material);
                    }
                }
            }

            Debug.Log(
                $"[ART-012] renderers {renderers.Count}, materials "
                + $"{before.Count} -> {after.Count} "
                + $"({consolidated} slots rebound), static {markedStatic}, "
                + $"shadows off {shadowsDisabled}.");
            return new Report(
                renderers.Count,
                before.Count,
                after.Count,
                markedStatic,
                shadowsDisabled);
        }

        /// <summary>
        /// Imported FBX materials arrive as one instance per file even when the
        /// colour is identical, which splits batches for no visual gain. Slots
        /// are rebound to a single representative per shader and colour.
        /// </summary>
        private static int ConsolidateMaterials(
            IReadOnlyList<Renderer> renderers)
        {
            var canonical = new Dictionary<string, Material>();
            int rebound = 0;

            foreach (Renderer renderer in renderers)
            {
                Material[] slots = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < slots.Length; index++)
                {
                    Material material = slots[index];
                    if (material == null || material.shader == null)
                    {
                        continue;
                    }

                    string key = BuildKey(material);
                    if (!canonical.TryGetValue(key, out Material chosen))
                    {
                        canonical.Add(key, material);
                        continue;
                    }

                    if (chosen == material)
                    {
                        continue;
                    }

                    slots[index] = chosen;
                    changed = true;
                    rebound++;
                }

                if (changed)
                {
                    renderer.sharedMaterials = slots;
                }
            }

            return rebound;
        }

        /// <summary>
        /// Identity is shader plus the properties that actually change the
        /// pixels here. Anything not compared stays distinct by remaining part
        /// of a different key only when it differs, so a false merge would need
        /// two materials that are genuinely identical.
        /// </summary>
        private static string BuildKey(Material material)
        {
            Color color = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color")
                    ? material.GetColor("_Color")
                    : Color.white;
            Texture texture = material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap")
                : material.HasProperty("_MainTex")
                    ? material.GetTexture("_MainTex")
                    : null;
            float metallic = material.HasProperty("_Metallic")
                ? material.GetFloat("_Metallic")
                : 0f;
            float smoothness = material.HasProperty("_Smoothness")
                ? material.GetFloat("_Smoothness")
                : 0f;

            string textureKey = texture != null
                ? texture.name
                : "none";
            return $"{material.shader.name}|{color}|{textureKey}"
                + $"|{metallic:0.###}|{smoothness:0.###}";
        }

        private static int MarkStatic(IReadOnlyList<Renderer> renderers)
        {
            int marked = 0;
            foreach (Renderer renderer in renderers)
            {
                if (IsUnderDynamicRoot(renderer.transform)
                    || renderer.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    continue;
                }

                GameObjectUtility.SetStaticEditorFlags(
                    renderer.gameObject,
                    StaticEditorFlags.BatchingStatic
                    | StaticEditorFlags.OccluderStatic
                    | StaticEditorFlags.OccludeeStatic
                    | StaticEditorFlags.ContributeGI);
                marked++;
            }

            return marked;
        }

        /// <summary>
        /// Props under roughly a metre cannot cast a shadow that reads at the
        /// fixed camera distance, so they stop paying for a shadow pass.
        /// Buildings keep theirs.
        /// </summary>
        private static int TrimShadows(IReadOnlyList<Renderer> renderers)
        {
            int disabled = 0;
            foreach (Renderer renderer in renderers)
            {
                if (renderer.shadowCastingMode
                    == UnityEngine.Rendering.ShadowCastingMode.Off)
                {
                    continue;
                }

                if (renderer.bounds.size.y > 1.1f
                    || IsUnderDynamicRoot(renderer.transform))
                {
                    continue;
                }

                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                disabled++;
            }

            return disabled;
        }

        private static bool IsUnderDynamicRoot(Transform candidate)
        {
            for (Transform current = candidate;
                current != null;
                current = current.parent)
            {
                foreach (string name in DynamicRootNames)
                {
                    if (current.name == name)
                    {
                        return true;
                    }
                }

                if (IsAnimatedByGreeter(current)
                    || IsAnimatedByDoor(current))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True for anything the raccoon greeter moves.
        ///
        /// Asked of the component rather than matched by name, because a name
        /// list fails silently: the lid was batched into the static mesh and
        /// simply stopped opening, with nothing in any log or test to say why.
        /// Whatever the greeter holds a reference to is excluded by
        /// construction, so renaming the objects cannot break it again.
        /// </summary>
        /// <summary>
        /// The same question for the house doors, and asked the same way.
        ///
        /// A baked door leaf turns on its hinge and stays shut on screen, with
        /// nothing in any log to say so — the exact failure the bin lid had.
        /// </summary>
        private static bool IsAnimatedByDoor(Transform candidate)
        {
            foreach (PawsAndLoot.Animation.HouseDoorLeaf leaf in
                Object.FindObjectsByType<
                    PawsAndLoot.Animation.HouseDoorLeaf>(
                    FindObjectsSortMode.None))
            {
                if (leaf.Hinge == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAnimatedByGreeter(Transform candidate)
        {
            foreach (RaccoonBinGreeter greeter in
                Object.FindObjectsByType<RaccoonBinGreeter>(
                    FindObjectsSortMode.None))
            {
                if (greeter.RaccoonRoot == candidate
                    || greeter.LidPivot == candidate)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
