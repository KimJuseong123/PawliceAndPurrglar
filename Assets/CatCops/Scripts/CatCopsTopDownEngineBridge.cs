#if PAWS_TOPDOWN_ENGINE_AVAILABLE
using MoreMountains.TopDownEngine;
#endif
using UnityEngine;

namespace CatCops
{
    public sealed class CatCopsTopDownEngineBridge : MonoBehaviour
    {
        public void SignalMatchStart()
        {
#if PAWS_TOPDOWN_ENGINE_AVAILABLE
            TopDownEngineEvent.Trigger(TopDownEngineEventTypes.LevelStart, null);
            TopDownEnginePointEvent.Trigger(PointsMethods.Set, 0);
#endif
        }

        public void SignalScore(int points)
        {
#if PAWS_TOPDOWN_ENGINE_AVAILABLE
            TopDownEnginePointEvent.Trigger(PointsMethods.Add, points);
            TopDownEngineEvent.Trigger(TopDownEngineEventTypes.Repaint, null);
#endif
        }

        public void SignalMatchEnd(bool policeWon)
        {
#if PAWS_TOPDOWN_ENGINE_AVAILABLE
            TopDownEngineEvent.Trigger(policeWon ? TopDownEngineEventTypes.LevelComplete : TopDownEngineEventTypes.GameOver, null);
#endif
        }
    }
}
