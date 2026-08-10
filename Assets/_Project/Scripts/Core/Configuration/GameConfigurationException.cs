using System;

namespace PawliceAndPurrglar.Config
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
