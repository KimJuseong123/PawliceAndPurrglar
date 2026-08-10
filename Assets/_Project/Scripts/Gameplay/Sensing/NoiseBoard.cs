using System;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Sensing
{
    /// <summary>
    /// Where loud things get written down.
    ///
    /// The town already had two ways for something to pull attention — a food
    /// lure that calls one named animal, and a torch that reveals — and neither
    /// is a sound. A sound is different in a way that matters: it does not care
    /// who made it or who it is for. Everybody near it turns to look, including
    /// the person who set it off, and that is exactly what makes a noise prop a
    /// tool rather than a targeted weapon.
    ///
    /// One board rather than an event per prop. The rubber chicken and the
    /// firework are the first two, but the design document has a bottle
    /// breaking, a gold bar dropped on tile, and a till drawer opening all
    /// making noise, and every one of those should reach the same ears by the
    /// same route.
    ///
    /// The host writes; the network layer repeats the entry to the client so
    /// both screens ring at the same place. Nothing here decides anything about
    /// the match — it is a record of what was heard, and the animals and the
    /// screen read it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoiseBoard : MonoBehaviour
    {
        /// <summary>
        /// How long an entry stays worth reacting to.
        ///
        /// A noise is an instant, but an instant cannot be noticed by anything
        /// that runs on frames, and an animal that turns toward a sound needs
        /// the sound to still be there when it finishes turning.
        /// </summary>
        public const float DefaultRingSeconds = 1.5f;

        private float _remaining;

        public event Action<NoiseReport> Heard;

        public NoiseReport Latest { get; private set; }
        public bool IsRinging => _remaining > 0f;
        public float RemainingSeconds => _remaining;

        /// <summary>
        /// How many noises have been written down. Latched rather than derived,
        /// so a test or a probe can tell "nothing was heard" from "something was
        /// heard and has since faded".
        /// </summary>
        public int ReportedCount { get; private set; }

        /// <summary>
        /// Writes a noise down. Later noises replace earlier ones rather than
        /// queueing: two bangs a moment apart are one thing to look at, and it
        /// is the second one.
        /// </summary>
        public void Report(NoiseReport report)
        {
            Latest = report;
            _remaining = Mathf.Max(0.01f, report.RingSeconds);
            ReportedCount++;
            Heard?.Invoke(report);
        }

        public void Report(
            Vector3 at,
            float radius,
            PlayerRole? madeBy,
            float ringSeconds = DefaultRingSeconds)
        {
            Report(new NoiseReport(at, radius, madeBy, ringSeconds));
        }

        public void Tick(float deltaTime)
        {
            if (_remaining <= 0f || deltaTime <= 0f)
            {
                return;
            }

            _remaining = Mathf.Max(0f, _remaining - deltaTime);
        }

        /// <summary>
        /// Whether a listener at this point would have heard the last noise.
        ///
        /// Flat range rather than a falloff. A sound that is faintly audible
        /// from far away is a sound the player cannot act on, and a prop whose
        /// effect fades out has no edge for anybody to learn.
        /// </summary>
        public bool IsAudibleAt(Vector3 listener)
        {
            if (!IsRinging)
            {
                return false;
            }

            Vector3 delta = listener - Latest.At;
            delta.y = 0f;
            return delta.sqrMagnitude <= Latest.Radius * Latest.Radius;
        }

        public void Clear()
        {
            _remaining = 0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }

    /// <summary>
    /// One thing somebody heard.
    /// </summary>
    public readonly struct NoiseReport
    {
        public NoiseReport(
            Vector3 at,
            float radius,
            PlayerRole? madeBy,
            float ringSeconds)
        {
            At = at;
            Radius = radius;
            MadeBy = madeBy;
            RingSeconds = ringSeconds;
        }

        public Vector3 At { get; }

        /// <summary>
        /// How far it carries, in metres.
        /// </summary>
        public float Radius { get; }

        /// <summary>
        /// Who set it off, when that is known.
        ///
        /// Recorded but deliberately not used to decide who reacts. It is here
        /// so the screen can tell a player "that was yours" rather than sending
        /// them running to their own firework.
        /// </summary>
        public PlayerRole? MadeBy { get; }

        public float RingSeconds { get; }
    }
}
