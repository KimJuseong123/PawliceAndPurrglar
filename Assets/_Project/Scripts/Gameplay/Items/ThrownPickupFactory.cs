using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// Builds the non-networked scene representation of a missed thrown item.
    ///
    /// The root reuses <see cref="ThrowablePickup"/> and gets one trigger-only
    /// sphere so the existing E-key scanner and prompt can find it. The visual
    /// is a runtime primitive and deliberately has no collider, Rigidbody or
    /// NetworkObject.
    /// </summary>
    public static class ThrownPickupFactory
    {
        public static ThrowablePickup Create(
            int pickupId,
            ThrowableKind kind,
            Vector3 position)
        {
            var root = new GameObject(
                $"Thrown Pickup {pickupId} ({kind})");
            root.transform.position = position;

            SphereCollider interactionTrigger =
                root.AddComponent<SphereCollider>();
            interactionTrigger.isTrigger = true;
            interactionTrigger.radius = 0.55f;
            interactionTrigger.center = Vector3.up * 0.4f;

            Transform visual = CreateVisual(root.transform, kind);
            PlayerRole? owner = ThrowableCatalog.GetOwner(kind);

            ThrowablePickup pickup =
                root.AddComponent<ThrowablePickup>();
            pickup.Configure(
                kind,
                visual,
                owner.HasValue,
                owner ?? PlayerRole.Thief,
                0f,
                pickupId);
            return pickup;
        }

        private static Transform CreateVisual(
            Transform parent,
            ThrowableKind kind)
        {
            GameObject visual = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            visual.name = "Runtime Thrown Pickup Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.up * 0.4f;
            visual.transform.localScale =
                Vector3.one * ThrowableCatalog.PropDiameterMeters;

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                // This primitive is presentation only. Remove its collider
                // immediately so the root remains the sole trigger collider.
                Object.DestroyImmediate(visualCollider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            Shader shader = Shader.Find(
                "Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (renderer != null && shader != null)
            {
                var material = new Material(shader)
                {
                    name = $"RuntimeThrownPickup_{kind}"
                };
                material.color = GetColor(kind);
                renderer.sharedMaterial = material;
            }

            if (renderer != null)
            {
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return visual.transform;
        }

        private static Color GetColor(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Bone => new Color(0.9f, 0.78f, 0.56f),
                ThrowableKind.TunaCan => new Color(0.38f, 0.7f, 0.82f),
                ThrowableKind.RubberChicken => new Color(1f, 0.82f, 0.18f),
                ThrowableKind.NoiseCan => new Color(0.75f, 0.82f, 0.88f),
                _ => new Color(0.35f, 0.35f, 0.38f)
            };
        }
    }
}
