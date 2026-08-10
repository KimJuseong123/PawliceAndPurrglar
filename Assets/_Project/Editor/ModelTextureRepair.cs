using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Gives the imported models their own textures back.
    ///
    /// Every model came out of Tripo with its texture baked into a single
    /// atlas, carried alongside the FBX in a folder named after it. Importing
    /// them we renamed the files — `jewelry+shop+3d+model.fbx` became
    /// `building_jewelry.fbx` — but the FBX records the texture by the path it
    /// had at export, `jewelry+shop+3d+model.fbm/..._basecolor.jpg`. That
    /// folder no longer exists under that name, so Unity finds no texture,
    /// falls back to a plain white material, and says nothing about it.
    ///
    /// Nine of the eleven textured models were affected. It read as an art
    /// problem — buildings blank white, roads flat grey — and it is not: the
    /// textures were sitting in the project the whole time, unreferenced. The
    /// road tile's atlas has its centre line and its crossing stripes painted
    /// on, which is why the streets looked bare.
    ///
    /// Rather than repair the paths inside the FBX files, this builds one
    /// material per model from the texture that is actually there and remaps
    /// the model onto it. The remap is stored in the importer, so it survives
    /// reimport, and the material is an asset on disk, so it survives a scene
    /// save — unlike a material made in memory, which is the trap that made
    /// the roads invisible once already.
    /// </summary>
    internal static class ModelTextureRepair
    {
        private const string ArtRoot = "Assets/_Project/Art";
        private const string MaterialRoot = "Assets/_Project/Materials/Models";

        [MenuItem("PawliceAndPurrglar/Setup/Repair Model Textures")]
        public static void Repair()
        {
            Directory.CreateDirectory(MaterialRoot);
            AssetDatabase.Refresh();

            int repaired = 0;
            int untextured = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string path in ModelPaths())
                {
                    string texture = BakedTextureFor(path);
                    if (texture == null)
                    {
                        untextured++;
                        continue;
                    }

                    if (Remap(path, texture))
                    {
                        repaired++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[ART] {repaired} models pointed back at their own texture. "
                + $"{untextured} carry no baked texture and were left alone.");
        }

        /// <summary>
        /// Checks that every model that ships a texture is using it.
        ///
        /// White is not an error state anything reports, so this asks the
        /// question directly: load the model, look at what its renderers were
        /// given, and complain about any that were handed nothing.
        /// </summary>
        [MenuItem("PawliceAndPurrglar/Setup/Validate Model Textures")]
        public static void Validate()
        {
            var blank = new List<string>();
            int textured = 0;

            foreach (string path in ModelPaths())
            {
                if (BakedTextureFor(path) == null)
                {
                    continue;
                }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    continue;
                }

                bool painted = model
                    .GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Any(material =>
                        material != null
                        && material.HasProperty(BaseMap)
                        && material.GetTexture(BaseMap) != null);

                if (painted)
                {
                    textured++;
                }
                else
                {
                    blank.Add(Path.GetFileNameWithoutExtension(path));
                }
            }

            if (blank.Count > 0)
            {
                Debug.LogError(
                    "[ART] These models ship a texture but are drawing white: "
                    + string.Join(", ", blank)
                    + ". Run Repair Model Textures.");
                return;
            }

            Debug.Log($"[ART] All {textured} textured models are using theirs.");
        }

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor =
            Shader.PropertyToID("_BaseColor");

        private static IEnumerable<string> ModelPaths()
        {
            return AssetDatabase
                .FindAssets("t:Model", new[] { ArtRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx"))
                .Distinct()
                .OrderBy(path => path);
        }

        /// <summary>
        /// The colour map that shipped with a model, or null if it has none.
        ///
        /// Looked up by the folder Unity would have created, not by the path
        /// inside the FBX — the whole problem is that those disagree.
        /// </summary>
        private static string BakedTextureFor(string modelPath)
        {
            string folder = Path.ChangeExtension(modelPath, null) + ".fbm";
            if (!Directory.Exists(folder))
            {
                return null;
            }

            return Directory
                .GetFiles(folder)
                .Where(file => !file.EndsWith(".meta"))
                .Select(file => file.Replace('\\', '/'))
                .OrderBy(file => file.Contains("basecolor") ? 0 : 1)
                .FirstOrDefault();
        }

        private static bool Remap(string modelPath, string texturePath)
        {
            var importer =
                AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            var texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                return false;
            }

            string stem = Path.GetFileNameWithoutExtension(modelPath);
            Material material = MaterialFor(stem, texture);

            // Every material the model declares gets the same atlas, because
            // that is how it was baked: one texture for the whole object, with
            // each part living in its own corner of it.
            bool changed = false;
            foreach (Object embedded in
                AssetDatabase.LoadAllAssetRepresentationsAtPath(modelPath))
            {
                if (embedded is not Material slot)
                {
                    continue;
                }

                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(
                        typeof(Material),
                        slot.name),
                    material);
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            importer.SaveAndReimport();
            return true;
        }

        private static Material MaterialFor(string stem, Texture2D texture)
        {
            string path = $"{MaterialRoot}/{stem}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture(BaseMap, texture);
            material.SetColor(BaseColor, Color.white);
            material.SetFloat(Shader.PropertyToID("_Smoothness"), 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
