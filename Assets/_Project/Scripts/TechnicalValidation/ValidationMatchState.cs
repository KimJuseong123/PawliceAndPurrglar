using PawliceAndPurrglar.Match;
using UnityEngine;

namespace PawliceAndPurrglar.TechnicalValidation
{
    /// <summary>
    /// Always-playing match state used only by the isolated validation scene.
    /// It lets the existing scanner and carrier run without bootstrapping the
    /// production lobby or network services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ValidationMatchState : MonoBehaviour, IMatchStateReader
    {
        public MatchState CurrentState => MatchState.Playing;
        public bool IsGameplayActive => isActiveAndEnabled;
    }
}
