using UnityEngine;

namespace PawliceAndPurrglar.Config
{
    public abstract class GameConfigAsset : ScriptableObject
    {
        public abstract void ValidateOrThrow();
    }
}
