#if CATCOPS_TOPDOWNENGINE
using MoreMountains.TopDownEngine;
#endif
using UnityEngine;

namespace CatCops
{
    /// <summary>
    /// Legacy prototype hook into TopDownEngine's event bus.
    ///
    /// TopDownEngine is a paid asset whose licence forbids redistribution, so it
    /// is excluded from this repository. Without the guard below, this one file
    /// stopped the entire project compiling on every fresh clone: the legacy
    /// prototype's dependency became everybody's build error.
    ///
    /// Set the <c>CATCOPS_TOPDOWNENGINE</c> scripting define symbol in a local
    /// checkout that has the asset installed to get the real signalling back.
    /// Everywhere else these calls are deliberately inert. Nothing in
    /// <c>Assets/_Project</c> references this class, and the current game does
    /// not use TopDownEngine at all.
    /// </summary>
    public sealed class CatCopsTopDownEngineBridge : MonoBehaviour
    {
        public void SignalMatchStart()
        {
#if CATCOPS_TOPDOWNENGINE
            TopDownEngineEvent.Trigger(
                TopDownEngineEventTypes.LevelStart,
                null);
            TopDownEnginePointEvent.Trigger(PointsMethods.Set, 0);
#endif
        }

        public void SignalScore(int points)
        {
#if CATCOPS_TOPDOWNENGINE
            TopDownEnginePointEvent.Trigger(PointsMethods.Add, points);
            TopDownEngineEvent.Trigger(
                TopDownEngineEventTypes.Repaint,
                null);
#else
            // Read so the parameter is not flagged unused, and so it is plain
            // that the signal is dropped on purpose rather than forgotten.
            _ = points;
#endif
        }

        public void SignalMatchEnd(bool policeWon)
        {
#if CATCOPS_TOPDOWNENGINE
            TopDownEngineEvent.Trigger(
                policeWon
                    ? TopDownEngineEventTypes.LevelComplete
                    : TopDownEngineEventTypes.GameOver,
                null);
#else
            _ = policeWon;
#endif
        }
    }
}
