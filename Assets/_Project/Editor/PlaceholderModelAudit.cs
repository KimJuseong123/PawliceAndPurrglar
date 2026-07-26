using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Reports the proportions of candidate placeholder character meshes so a
    /// stand-in can be chosen on measurements instead of guesswork. The
    /// project wants a two-heads-tall cartoon silhouette, which needs the head
    /// renderer height compared against the whole model height.
    /// </summary>
    public static class PlaceholderModelAudit
    {
        private static readonly string[] AuthoredPaths =
        {
            "Assets/_Project/Art/Characters/police.fbx",
            "Assets/_Project/Art/Characters/thief.fbx",
            "Assets/_Project/Art/Characters/dog.fbx",
            "Assets/_Project/Art/Characters/cat.fbx",
            "Assets/_Project/Art/Characters/raccoon.fbx",
            "Assets/_Project/Art/Props/trashcan_lid.fbx",
            "Assets/_Project/Art/Buildings/building_police_station.fbx",
            "Assets/_Project/Art/Buildings/building_supermarket.fbx",
            "Assets/_Project/Art/Buildings/building_bookstore.fbx",
            "Assets/_Project/Art/Buildings/building_house_1f.fbx",
            "Assets/_Project/Art/Buildings/"
            + "building_house_1f_with_interior.fbx"
        };

        [MenuItem("Paws & Loot/Setup/Audit Authored Models")]
        public static void AuditAuthored()
        {
            foreach (string path in AuthoredPaths)
            {
                GameObject asset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    Debug.Log($"[Audit] MISSING {path}");
                    continue;
                }

                GameObject instance = Object.Instantiate(asset);
                try
                {
                    Report(path, instance);
                    ReportClips(path);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static void ReportClips(string path)
        {
            var clips = new List<string>();
            foreach (Object sub in
                AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub is AnimationClip clip
                    && !clip.name.StartsWith("__preview__"))
                {
                    clips.Add($"{clip.name}({clip.length:0.00}s)");
                }
            }

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            string rig = importer != null
                ? importer.animationType.ToString()
                : "unknown";
            string scale = importer != null
                ? importer.globalScale.ToString("0.###")
                : "?";
            Debug.Log(
                $"[Audit]   rig={rig} importScale={scale} "
                + $"clips={(clips.Count == 0 ? "none" : string.Join(", ", clips))}");
        }

        private static readonly string[] CandidatePaths =
        {
            "Assets/TopDownEngine/Demos/Explodudes/Models/"
            + "ExplodudePrototype.fbx",
            "Assets/TopDownEngine/Demos/Explodudes/Animations/Characters/"
            + "MM/MMExplodude.fbx",
            "Assets/TopDownEngine/Demos/Minimal3D/Models/MinimalDude.fbx",
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/"
            + "LoftTie.fbx",
            "Assets/TopDownEngine/Demos/Loft3D/Models/Characters/"
            + "Suspenders/LoftSuspenders.fbx",
            "Assets/TopDownEngine/Demos/Colonel/Models/Colonel@T-Pose.fbx",
            "Assets/TopDownEngine/ThirdParty/MoreMountains/MMFeedbacks/"
            + "Demos/MMFeedbacksDemo/Models/Dude/"
            + "MMFeedbackDemoDude@TPose.fbx"
        };

        [MenuItem("Paws & Loot/Setup/Audit Placeholder Character Models")]
        public static void Audit()
        {
            foreach (string path in CandidatePaths)
            {
                GameObject asset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    Debug.Log($"[Audit] MISSING {path}");
                    continue;
                }

                GameObject instance = Object.Instantiate(asset);
                try
                {
                    Report(path, instance);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static void Report(string path, GameObject instance)
        {
            var renderers =
                new List<Renderer>(
                    instance.GetComponentsInChildren<Renderer>(true));
            if (renderers.Count == 0)
            {
                Debug.Log($"[Audit] NO RENDERER {path}");
                return;
            }

            Bounds total = renderers[0].bounds;
            for (int index = 1; index < renderers.Count; index++)
            {
                total.Encapsulate(renderers[index].bounds);
            }

            var parts = new List<string>();
            float headHeight = 0f;
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    string materialName = material != null
                        ? material.name
                        : "<none>";
                    parts.Add(
                        $"{renderer.name}:{materialName}"
                        + $"(h={renderer.bounds.size.y:0.00})");
                    if (materialName.ToLowerInvariant().Contains("head"))
                    {
                        headHeight = Mathf.Max(
                            headHeight,
                            renderer.bounds.size.y);
                    }
                }
            }

            string headInfo = headHeight > 0.001f
                ? $"head={headHeight:0.00}m ratio="
                  + $"{total.size.y / headHeight:0.0} heads"
                : "head=unknown";
            int boneCount = 0;
            foreach (SkinnedMeshRenderer skinned in
                instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                boneCount = Mathf.Max(boneCount, skinned.bones.Length);
            }

            var boneNames = new List<string>();
            foreach (SkinnedMeshRenderer skinned in
                instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (Transform bone in skinned.bones)
                {
                    if (bone != null && !boneNames.Contains(bone.name))
                    {
                        boneNames.Add(bone.name);
                    }
                }
            }

            Debug.Log(
                $"[Audit] {path}\n"
                + $"  height={total.size.y:0.00}m "
                + $"width={total.size.x:0.00}m depth={total.size.z:0.00}m\n"
                + $"  {headInfo} bones={boneCount} "
                + $"renderers={renderers.Count}\n"
                + $"  parts={string.Join(", ", parts)}\n"
                + $"  boneNames={string.Join(", ", boneNames)}");
        }
    }
}
