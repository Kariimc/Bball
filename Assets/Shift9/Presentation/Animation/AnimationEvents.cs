using Shift9.Sim.Match;

namespace Shift9.Presentation.Animation
{
    public enum PlayerActionTrigger : byte { None, Shoot, Pass, Rebound }

    /// <summary>Pure mapping from a sim possession event to the one-shot animation trigger it fires.</summary>
    public static class AnimationEvents
    {
        public static PlayerActionTrigger TriggerFor(PossessionEvent e)
        {
            switch (e)
            {
                case PossessionEvent.ShotReleased: return PlayerActionTrigger.Shoot;
                case PossessionEvent.Pass:         return PlayerActionTrigger.Pass;
                case PossessionEvent.ShotMissed:   return PlayerActionTrigger.Rebound;
                default:                           return PlayerActionTrigger.None;
            }
        }
    }
}
