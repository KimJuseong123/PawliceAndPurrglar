using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Interiors
{
    /// <summary>
    /// Puts a character down somewhere, correctly.
    ///
    /// Pulled out of the doorway because a second way out of a building was
    /// added and it needed exactly the same six careful steps. Two copies of
    /// this arithmetic would be two chances to fix one of them and not the
    /// other, and the bugs it avoids are all of the silent kind — the character
    /// buried to the knees, or sinking through the floor a few doors later.
    /// </summary>
    public static class InteriorTravel
    {
        /// <summary>
        /// Moves a character to a spot on a floor, optionally turning them.
        /// </summary>
        public static void Place(
            GameObject who,
            Vector3 destination,
            Quaternion? facing)
        {
            if (who == null)
            {
                return;
            }

            Transform player = who.transform;
            var controller = who.GetComponent<CharacterController>();
            bool hadController = controller != null && controller.enabled;
            if (hadController)
            {
                controller.enabled = false;
            }

            // Feet on the floor, not the pivot on the floor.
            //
            // A character's transform sits about 0.9 m above their soles, so
            // putting the transform on the surface buries the whole capsule in
            // it. That is nearly a metre of penetration for the controller to
            // sort out on the frame it wakes up, and it sorts it out downward.
            //
            // From the capsule's own numbers, not its bounds: the controller
            // has just been switched off and a disabled collider's bounds are
            // not reliable. The two characters are different models and neither
            // figure belongs written down here.
            float feetToPivot = controller != null
                ? (controller.height * 0.5f - controller.center.y)
                    * Mathf.Abs(player.lossyScale.y)
                : 0f;
            player.position = destination
                + Vector3.up * (feetToPivot + 0.06f);

            if (facing.HasValue)
            {
                player.rotation = facing.Value;
            }

            if (hadController)
            {
                controller.enabled = true;
            }

            Physics.SyncTransforms();

            // The fall speed has to stay with the old position. Gravity
            // accumulates every frame the controller is not grounded, and a
            // teleport leaves it briefly airborne; carried across, that speed
            // eventually takes the capsule through a floor between two frames.
            who.GetComponent<PlayerMovementMotor>()?.ResetVerticalVelocity();
        }
    }
}
