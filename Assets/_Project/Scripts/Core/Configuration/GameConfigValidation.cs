using UnityEngine;

namespace PawsAndLoot.Config
{
    internal static class GameConfigValidation
    {
        public static void RequireAssigned(Object owner, Object value, string fieldName)
        {
            if (value == null)
            {
                throw CreateException(owner, fieldName, "a required asset reference is missing");
            }
        }

        public static void RequirePositive(Object owner, float value, string fieldName)
        {
            if (value <= 0f)
            {
                throw CreateException(owner, fieldName, $"expected a value greater than 0, received {value}");
            }
        }

        public static void RequirePositive(Object owner, int value, string fieldName)
        {
            if (value <= 0)
            {
                throw CreateException(owner, fieldName, $"expected a value greater than 0, received {value}");
            }
        }

        public static void RequireNonNegative(Object owner, float value, string fieldName)
        {
            if (value < 0f)
            {
                throw CreateException(owner, fieldName, $"expected a value of 0 or greater, received {value}");
            }
        }

        public static void RequireTrue(Object owner, bool value, string fieldName, string reason)
        {
            if (!value)
            {
                throw CreateException(owner, fieldName, reason);
            }
        }

        public static GameConfigurationException CreateException(
            Object owner,
            string fieldName,
            string reason)
        {
            string ownerName = owner == null ? "<missing asset>" : owner.name;
            return new GameConfigurationException(
                $"Invalid game configuration in '{ownerName}.{fieldName}': {reason}.");
        }
    }
}
