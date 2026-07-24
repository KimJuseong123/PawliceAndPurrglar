using System;

namespace PawsAndLoot.Config
{
    public sealed class GameConfigurationException : InvalidOperationException
    {
        public GameConfigurationException(string message)
            : base(message)
        {
        }

        public GameConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
