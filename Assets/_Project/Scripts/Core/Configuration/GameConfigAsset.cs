using UnityEngine;

namespace PawsAndLoot.Config
{
    public abstract class GameConfigAsset : ScriptableObject
    {
        public abstract void ValidateOrThrow();
    }
}
