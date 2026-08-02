using System.Collections.Generic;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    /// <summary>
    /// The outskirt corners a thief may appear at, and the draw between them.
    ///
    /// One fixed spawn meant the officer knew where the match began and where
    /// every release would put the thief back. Knowing that is worth more than
    /// any amount of speed: the first ten seconds of a match, and the first ten
    /// after each of three arrests, stopped being a search.
    ///
    /// **Only ever drawn on one machine.** The thief's position is replicated
    /// host to client already, so the host drawing and the client following is
    /// the whole mechanism — no new message, no new variable. Two machines each
    /// calling this would each get a different corner and disagree about where
    /// the thief is, which is the failure this project keeps meeting from
    /// different directions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThiefSpawnPoints : MonoBehaviour
    {
        [SerializeField]
        private List<Transform> points = new();

        public IReadOnlyList<Transform> Points => points;

        public void Configure(IEnumerable<Transform> configuredPoints)
        {
            points = new List<Transform>();
            foreach (Transform point in configuredPoints)
            {
                if (point != null)
                {
                    points.Add(point);
                }
            }
        }

        /// <summary>
        /// Draws a corner. Falls back to the given position when the list is
        /// empty, so a scene that has not been furnished yet still starts.
        /// </summary>
        public Vector3 Draw(Vector3 fallback)
        {
            var live = new List<Transform>();
            foreach (Transform point in points)
            {
                if (point != null)
                {
                    live.Add(point);
                }
            }

            if (live.Count == 0)
            {
                return fallback;
            }

            return live[Random.Range(0, live.Count)].position;
        }
    }
}
