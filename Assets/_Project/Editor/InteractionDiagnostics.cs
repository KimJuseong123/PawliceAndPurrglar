using PawsAndLoot.Config;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Reports why an interaction target is or is not reachable.
    ///
    /// The scanner finds targets with a physics overlap, so a target needs an
    /// enabled collider inside the interaction radius and a permitted type.
    /// This dumps all three facts per target instead of guessing.
    /// </summary>
    public static class InteractionDiagnostics
    {
        [MenuItem("Pawlice and Purrglar/Setup/Diagnose Interaction Targets")]
        public static void Diagnose()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);

            PlayerConfig config =
                AssetDatabase.LoadAssetAtPath<PlayerConfig>(
                    "Assets/_Project/Settings/Configs/PlayerConfig.asset");
            float range = config != null ? config.InteractionRange : -1f;
            Debug.Log($"[Interaction] InteractionRange = {range:0.00}m");

            var police = Vector3.zero;
            var thief = Vector3.zero;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PlayerRoleIdentity identity in
                    root.GetComponentsInChildren<PlayerRoleIdentity>(true))
                {
                    if (identity.Role == PlayerRole.Police)
                    {
                        police = identity.transform.position;
                    }
                    else
                    {
                        thief = identity.transform.position;
                    }
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour component in
                    root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component is not IPlayerInteractable target)
                    {
                        continue;
                    }

                    Collider collider =
                        component.GetComponentInChildren<Collider>(true);
                    Vector3 position = target.InteractionTransform.position;
                    string colliderInfo = collider == null
                        ? "NO COLLIDER (unreachable)"
                        : $"collider={collider.GetType().Name} "
                          + $"enabled={collider.enabled} "
                          + $"trigger={collider.isTrigger}";

                    Debug.Log(
                        $"[Interaction] {component.gameObject.name}\n"
                        + $"  type={target.InteractionType} "
                        + $"available={target.IsAvailable}\n"
                        + $"  pos={position} {colliderInfo}\n"
                        + $"  distToPolice={Vector3.Distance(position, police):0.00}m "
                        + $"distToThief={Vector3.Distance(position, thief):0.00}m\n"
                        + $"  policeAllowed="
                        + $"{PlayerRolePermissions.CanInteract(PlayerRole.Police, target.InteractionType)} "
                        + $"thiefAllowed="
                        + $"{PlayerRolePermissions.CanInteract(PlayerRole.Thief, target.InteractionType)}");
                }
            }
        }
    }
}
