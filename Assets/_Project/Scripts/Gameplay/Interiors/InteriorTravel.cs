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
        /// <param name="lift">
        /// Clearance left between the soles and the surface. The default is the
        /// six centimetres a doorway wants — a character placed exactly on a
        /// floor starts the frame touching it, and the controller's first move
        /// then has to resolve a contact rather than a gap.
        ///
        /// Zero for anything restoring a position it read off a transform
        /// earlier: that point was already a valid pivot, and adding clearance to
        /// it each time makes a player who hides in the same crate twenty times
        /// climb out a metre in the air.
        /// </param>
        public static void Place(
            GameObject who,
            Vector3 destination,
            Quaternion? facing,
            float lift = 0.06f)
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
            float feetToPivot = FeetToPivot(controller, player);
            player.position = destination
                + Vector3.up * (feetToPivot + lift);

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

        /// <summary>
        /// How far a character's transform sits above their soles.
        ///
        /// Public because anybody who has a floor height and wants a transform
        /// position needs this number, and the alternative is each caller
        /// writing the arithmetic out again. The one place that did not — the
        /// hiding spots — put the pivot on the berth instead of the feet and
        /// buried the capsule a metre into the ground.
        /// </summary>
        public static float FeetToPivot(
            CharacterController controller,
            Transform player)
        {
            if (controller == null)
            {
                return 0f;
            }

            float scale = player != null
                ? Mathf.Abs(player.lossyScale.y)
                : 1f;
            return (controller.height * 0.5f - controller.center.y) * scale;
        }
    }
}
