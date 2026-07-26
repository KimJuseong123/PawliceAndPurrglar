using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Checks whether the authored character rigs can be imported as Unity
    /// Humanoid. A valid Avatar is the only way to retarget existing humanoid
    /// run clips onto them, because the authored FBX ship no clips of their
    /// own and Generic rigs cannot share animation across skeletons.
    /// </summary>
    public static class CharacterRigProbe
    {
        private static readonly string[] HumanoidCandidates =
        {
            "Assets/_Project/Art/Characters/police.fbx",
            "Assets/_Project/Art/Characters/thief.fbx"
        };

        [MenuItem("Paws & Loot/Setup/Probe Character Humanoid Rigs")]
        public static void Probe()
        {
            foreach (string path in HumanoidCandidates)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.Log($"[RigProbe] MISSING {path}");
                    continue;
                }

                ModelImporterAnimationType original =
                    importer.animationType;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.SaveAndReimport();

                Avatar avatar = null;
                foreach (Object sub in
                    AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (sub is Avatar candidate)
                    {
                        avatar = candidate;
                        break;
                    }
                }

                bool valid = avatar != null && avatar.isValid;
                Debug.Log(
                    $"[RigProbe] {path} humanoid={valid} "
                    + $"avatar={(avatar == null ? "none" : avatar.name)} "
                    + $"human={(avatar != null && avatar.isHuman)}");

                if (!valid)
                {
                    importer.animationType = original;
                    importer.SaveAndReimport();
                    Debug.Log(
                        $"[RigProbe] reverted {path} to {original}");
                }
            }
        }
    }
}
