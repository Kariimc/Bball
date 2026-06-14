using NUnit.Framework;
using Shift9.Presentation.Animation;
using Shift9.Sim.Match;

namespace Shift9.Presentation.Tests
{
    public sealed class AnimationEventsTests
    {
        [Test]
        public void TriggerFor_MapsActionsAndIgnoresTheRest()
        {
            Assert.AreEqual(PlayerActionTrigger.Shoot, AnimationEvents.TriggerFor(PossessionEvent.ShotReleased));
            Assert.AreEqual(PlayerActionTrigger.Pass, AnimationEvents.TriggerFor(PossessionEvent.Pass));
            Assert.AreEqual(PlayerActionTrigger.Rebound, AnimationEvents.TriggerFor(PossessionEvent.ShotMissed));
            Assert.AreEqual(PlayerActionTrigger.None, AnimationEvents.TriggerFor(PossessionEvent.ShotMade));
            Assert.AreEqual(PlayerActionTrigger.None, AnimationEvents.TriggerFor(PossessionEvent.None));
        }
    }
}
