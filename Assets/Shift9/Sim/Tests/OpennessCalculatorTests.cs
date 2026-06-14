using NUnit.Framework;
using Shift9.Sim.Defense;
using UnityEngine;

namespace Shift9.Sim.Tests
{
    public sealed class OpennessCalculatorTests
    {
        private static readonly Vector3 Shooter = new Vector3(0f, 0f, -20f); // home end
        private const bool Home = true;

        private static DefenderState[] One(Vector3 pos, byte rating) =>
            new[] { new DefenderState(pos, rating) };

        [Test]
        public void NoDefenders_IsWideOpen()
        {
            Assert.AreEqual(1f, OpennessCalculator.Compute(Shooter, Home, null));
            Assert.AreEqual(1f, OpennessCalculator.Compute(Shooter, Home, new DefenderState[0]));
        }

        [Test]
        public void DefenderInFace_IsTightlyContested()
        {
            // 2 ft toward the rim, directly in the lane.
            float o = OpennessCalculator.Compute(Shooter, Home, One(new Vector3(0, 0, -22f), 80));
            Assert.Less(o, 0.3f);
        }

        [Test]
        public void DistantDefender_StaysOpen()
        {
            float o = OpennessCalculator.Compute(Shooter, Home, One(new Vector3(0, 0, -35f), 80));
            Assert.Greater(o, 0.85f);
        }

        [Test]
        public void CloserDefenderReducesOpenness()
        {
            float near = OpennessCalculator.Compute(Shooter, Home, One(new Vector3(0, 0, -23f), 75));
            float far = OpennessCalculator.Compute(Shooter, Home, One(new Vector3(0, 0, -28f), 75));
            Assert.Less(near, far);
        }

        [Test]
        public void InLaneContestsMoreThanOffLane()
        {
            float inLane = OpennessCalculator.Compute(Shooter, Home, One(new Vector3(0, 0, -22f), 75));  // toward rim
            float offLane = OpennessCalculator.Compute(Shooter, Home, One(new Vector3(2f, 0, -20f), 75)); // beside
            Assert.Less(inLane, offLane);
        }

        [Test]
        public void BetterDefenderReducesOpenness()
        {
            float weak = OpennessCalculator.Compute(Shooter, Home, One(new Vector3(0, 0, -22.5f), 40));
            float elite = OpennessCalculator.Compute(Shooter, Home, One(new Vector3(0, 0, -22.5f), 95));
            Assert.Less(elite, weak);
        }

        [Test]
        public void StrongestDefenderSetsOpenness()
        {
            // A wide-open-from-one but smothered-by-another situation tracks the smotherer.
            var defs = new[]
            {
                new DefenderState(new Vector3(0, 0, -35f), 80),  // far
                new DefenderState(new Vector3(0, 0, -22f), 80),  // in face
            };
            float two = OpennessCalculator.Compute(Shooter, Home, defs);
            float justClose = OpennessCalculator.Compute(Shooter, Home, One(new Vector3(0, 0, -22f), 80));
            Assert.AreEqual(justClose, two, 0.0001f);
        }
    }
}
