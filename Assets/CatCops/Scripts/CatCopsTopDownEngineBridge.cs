using MoreMountains.TopDownEngine;
using UnityEngine;

namespace CatCops
{
    public sealed class CatCopsTopDownEngineBridge : MonoBehaviour
    {
        public void SignalMatchStart()
        {
            TopDownEngineEvent.Trigger(TopDownEngineEventTypes.LevelStart, null);
            TopDownEnginePointEvent.Trigger(PointsMethods.Set, 0);
        }

        public void SignalScore(int points)
        {
            TopDownEnginePointEvent.Trigger(PointsMethods.Add, points);
            TopDownEngineEvent.Trigger(TopDownEngineEventTypes.Repaint, null);
        }

        public void SignalMatchEnd(bool policeWon)
        {
            TopDownEngineEvent.Trigger(policeWon ? TopDownEngineEventTypes.LevelComplete : TopDownEngineEventTypes.GameOver, null);
        }
    }
}
