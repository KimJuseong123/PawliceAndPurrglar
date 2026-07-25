using NUnit.Framework;
using PawsAndLoot.Config;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class PlayerConfigTests
    {
        [Test]
        public void DefaultCarryMultiplierIsTenPercentPenalty()
        {
            PlayerConfig config =
                ScriptableObject.CreateInstance<PlayerConfig>();

            Assert.That(
                config.LootCarrySpeedMultiplier,
                Is.EqualTo(0.9f).Within(0.001f));
            Assert.DoesNotThrow(config.ValidateOrThrow);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void CarryMultiplierAboveOneIsRejected()
        {
            PlayerConfig config =
                ScriptableObject.CreateInstance<PlayerConfig>();
            var serialized = new SerializedObject(config);
            serialized.FindProperty("lootCarrySpeedMultiplier")
                .floatValue = 1.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<GameConfigurationException>(
                config.ValidateOrThrow);

            Object.DestroyImmediate(config);
        }
    }
}
