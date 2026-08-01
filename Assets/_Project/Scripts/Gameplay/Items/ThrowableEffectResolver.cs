using System;
using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
{
    [Flags]
    public enum ThrowableEffectTarget
    {
        None = 0,
        Human = 1,
        Dog = 2,
        Cat = 4
    }

    [Flags]
    public enum ThrowableActivationType
    {
        None = 0,
        DirectHit = 1,
        Landing = 2
    }

    public enum ThrowableEffectType
    {
        None = 0,
        Stun = 1,
        Attraction = 2,
        Confuse = 3,
        Noise = 4
    }

    public readonly struct ThrowableEffectProfile
    {
        public ThrowableEffectProfile(
            ThrowableEffectTarget effectTargets,
            ThrowableActivationType activationTypes,
            ThrowableEffectType effects,
            float radius,
            float duration,
            float strength)
        {
            EffectTargets = effectTargets;
            ActivationTypes = activationTypes;
            Effects = effects;
            Radius = radius;
            Duration = duration;
            Strength = strength;
        }

        public ThrowableEffectTarget EffectTargets { get; }
        public ThrowableActivationType ActivationTypes { get; }
        public ThrowableEffectType Effects { get; }
        public float Radius { get; }
        public float Duration { get; }
        public float Strength { get; }
    }

    /// <summary>
    /// Host-side target/effect resolution for thrown props. Direct hits and
    /// landing effects are separate entry points so an attraction item cannot
    /// accidentally stun a character it merely lands beside.
    /// </summary>
    public static class ThrowableEffectResolver
    {
        public static ThrowableEffectProfile GetProfile(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Bone => new ThrowableEffectProfile(
                    ThrowableEffectTarget.Dog,
                    ThrowableActivationType.DirectHit
                        | ThrowableActivationType.Landing,
                    ThrowableEffectType.Attraction,
                    7f,
                    6f,
                    8f),
                ThrowableKind.TunaCan => new ThrowableEffectProfile(
                    ThrowableEffectTarget.Cat,
                    ThrowableActivationType.DirectHit
                        | ThrowableActivationType.Landing,
                    ThrowableEffectType.Attraction,
                    6f,
                    6f,
                    8f),
                ThrowableKind.RubberChicken => new ThrowableEffectProfile(
                    ThrowableEffectTarget.Dog,
                    ThrowableActivationType.DirectHit
                        | ThrowableActivationType.Landing,
                    ThrowableEffectType.Confuse,
                    5f,
                    3f,
                    4f),
                ThrowableKind.NoiseCan => new ThrowableEffectProfile(
                    ThrowableEffectTarget.Human
                        | ThrowableEffectTarget.Dog
                        | ThrowableEffectTarget.Cat,
                    ThrowableActivationType.DirectHit
                        | ThrowableActivationType.Landing,
                    ThrowableEffectType.Noise
                        | ThrowableEffectType.Stun,
                    5f,
                    1.2f,
                    5f),
                _ => new ThrowableEffectProfile(
                    ThrowableEffectTarget.Human,
                    ThrowableActivationType.DirectHit,
                    ThrowableEffectType.Stun,
                    0f,
                    ThrowableCatalog.GetStunSeconds(kind),
                    1f)
            };
        }

        public static void ApplyDirectHit(
            ThrowableKind kind,
            PlayerRoleIdentity victim,
            PlayerRoleIdentity thrower)
        {
            if (victim == null)
            {
                return;
            }

            ThrowableEffectProfile profile = GetProfile(kind);
            if (kind == ThrowableKind.Rock
                || (profile.Effects & ThrowableEffectType.Stun) != 0)
            {
                StunState stun = victim.GetComponent<StunState>();
                if (stun?.TryApply(profile.Duration) == true
                    && kind == ThrowableKind.Rock)
                {
                    PawsAndLoot.Gameplay.Loot.LootConfiscationRule.Apply(
                        victim,
                        thrower);
                }
            }

            if (kind == ThrowableKind.NoiseCan)
            {
                ApplyNoise(victim.transform.position, profile);
            }
        }

        public static void ApplyLanding(
            ThrowableKind kind,
            Vector3 position,
            PlayerRoleIdentity thrower)
        {
            ThrowableEffectProfile profile = GetProfile(kind);
            if ((profile.ActivationTypes & ThrowableActivationType.Landing) == 0)
            {
                return;
            }

            if ((profile.Effects & ThrowableEffectType.Noise) != 0)
            {
                ApplyNoise(position, profile);
            }

            foreach (CompanionAgent agent in UnityEngine.Object.FindObjectsByType<
                         CompanionAgent>(FindObjectsSortMode.None))
            {
                if (!agent.isActiveAndEnabled
                    || Vector3.Distance(
                        Planar(position),
                        Planar(agent.transform.position)) > profile.Radius)
                {
                    continue;
                }

                bool dog = agent.CompanionKind == CompanionKind.Dog;
                bool cat = agent.CompanionKind == CompanionKind.Cat;
                bool validTarget = dog
                    ? (profile.EffectTargets & ThrowableEffectTarget.Dog) != 0
                    : cat
                        && (profile.EffectTargets
                            & ThrowableEffectTarget.Cat) != 0;
                if (!validTarget)
                {
                    continue;
                }

                if ((profile.Effects & ThrowableEffectType.Attraction) != 0)
                {
                    agent.ReceiveAttraction(
                        kind,
                        position,
                        profile.Strength,
                        profile.Duration);
                }
                else if ((profile.Effects & ThrowableEffectType.Confuse) != 0)
                {
                    agent.ReceiveConfusion(profile.Duration);
                }
            }
        }

        private static void ApplyNoise(
            Vector3 position,
            ThrowableEffectProfile profile)
        {
            foreach (CompanionAgent agent in UnityEngine.Object.FindObjectsByType<
                         CompanionAgent>(FindObjectsSortMode.None))
            {
                if (agent.isActiveAndEnabled
                    && Vector3.Distance(
                        Planar(position),
                        Planar(agent.transform.position)) <= profile.Radius)
                {
                    agent.ReceiveNoise(position, profile.Strength, profile.Duration);
                }
            }
        }

        private static Vector3 Planar(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
