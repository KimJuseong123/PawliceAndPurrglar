using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// Moves a thing on a client toward where the host says it is.
    ///
    /// One copy of the arithmetic, because there was more than one and they
    /// drifted. The players were moved off <c>MoveTowards</c> when the guest
    /// reported juddering, and the animals — replicated the same way, in the
    /// same file, twenty lines apart — were left on it. Months later the cat
    /// shook on the client while the thief carrying it glided, and the fix was
    /// the one already sitting above it.
    ///
    /// Two rules and nothing else:
    ///
    /// - Far away is a teleport, so go there. The owner walks through a door
    ///   and the host's animal is in a room hundreds of metres off the map;
    ///   easing that is a walk that lasts the rest of the match.
    /// - Near is lag, so ease. An exponential approach never arrives and never
    ///   stalls, which is what keeps the motion continuous. Anything that can
    ///   arrive early will then wait for the next packet, and moving on half
    ///   the frames is what a judder is.
    /// </summary>
    public static class ReplicatedFollow
    {
        public static Vector3 Step(
            Vector3 current,
            Vector3 told,
            ref Vector3 velocity,
            float snapDistance,
            float smoothSeconds,
            float catchUpSpeed,
            float deltaTime)
        {
            if (Vector3.Distance(current, told) > snapDistance)
            {
                velocity = Vector3.zero;
                return told;
            }

            return Vector3.SmoothDamp(
                current,
                told,
                ref velocity,
                smoothSeconds,
                catchUpSpeed,
                deltaTime);
        }
    }
}
