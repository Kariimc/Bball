using NUnit.Framework;
using Shift9.Presentation.Animation;

namespace Shift9.Presentation.Tests
{
    public sealed class LocomotionMapperTests
    {
        [Test]
        public void Normalize_ScalesAndClamps()
        {
            Assert.AreEqual(0f, LocomotionMapper.Normalize(0f, 14f), 0.0001f);
            Assert.AreEqual(0.5f, LocomotionMapper.Normalize(7f, 14f), 0.0001f);
            Assert.AreEqual(1f, LocomotionMapper.Normalize(14f, 14f), 0.0001f);
            Assert.AreEqual(1f, LocomotionMapper.Normalize(20f, 14f), 0.0001f); // clamps high
            Assert.AreEqual(0f, LocomotionMapper.Normalize(-3f, 14f), 0.0001f); // clamps low
            Assert.AreEqual(0f, LocomotionMapper.Normalize(5f, 0f), 0.0001f);   // guards zero max
        }

        [Test]
        public void Classify_ThresholdsIdleJogSprint()
        {
            Assert.AreEqual(LocomotionState.Idle, LocomotionMapper.Classify(0f));
            Assert.AreEqual(LocomotionState.Idle, LocomotionMapper.Classify(0.04f));
            Assert.AreEqual(LocomotionState.Jog, LocomotionMapper.Classify(0.05f));
            Assert.AreEqual(LocomotionState.Jog, LocomotionMapper.Classify(0.69f));
            Assert.AreEqual(LocomotionState.Sprint, LocomotionMapper.Classify(0.7f));
            Assert.AreEqual(LocomotionState.Sprint, LocomotionMapper.Classify(1f));
        }
    }
}
