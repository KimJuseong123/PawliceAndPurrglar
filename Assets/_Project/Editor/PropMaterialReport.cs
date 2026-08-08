using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Says which material and texture each prop prefab actually carries.
    ///
    /// A model can be remapped in its importer, own a material, and have that
    /// material own a texture, and still render grey — the three facts are stored
    /// in three places and only the renderer's opinion is the one on screen.
    /// </summary>
    internal static class PropMaterialReport
    {
        [MenuItem("Pawlice and Purrglar/Setup/Report Prop Materials")]
        public static void Report()
        {
            foreach (string path in AssetDatabase
                .FindAssets("t:Prefab t:Model", new[] { "Assets/_Project/Resources/Props", "Assets/_Project/Art/Props" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (Renderer part in
                    asset.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in part.sharedMaterials)
                    {
                        Texture texture = material == null
                            ? null
                            : material.HasProperty("_BaseMap")
                                ? material.GetTexture("_BaseMap")
                                : null;
                        Debug.Log(
                            $"[MAT] {System.IO.Path.GetFileNameWithoutExtension(path)}"
                            + $" -> material '{(material == null ? "NONE" : material.name)}'"
                            + $" shader '{(material == null ? "-" : material.shader.name)}'"
                            + $" baseMap '{(texture == null ? "NONE" : texture.name)}'"
                            + $" colour {(material == null ? "-" : material.GetColor("_BaseColor").ToString())}"
                            + $" size {(texture == null ? "-" : $"{texture.width}x{texture.height}")}"
                            + $" mesh '{(part is MeshRenderer mr && mr.GetComponent<MeshFilter>() != null && mr.GetComponent<MeshFilter>().sharedMesh != null ? mr.GetComponent<MeshFilter>().sharedMesh.name : "?")}'"
                            + $" submeshes {(part is MeshRenderer m2 && m2.GetComponent<MeshFilter>()?.sharedMesh != null ? m2.GetComponent<MeshFilter>().sharedMesh.subMeshCount : -1)}"
                            + $" slots {part.sharedMaterials.Length}");
                    }
                }
            }
        }
    }
}
