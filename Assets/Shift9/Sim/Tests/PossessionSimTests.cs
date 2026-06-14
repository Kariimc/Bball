using NUnit.Framework;
using Shift9.Sim.Core;
using Shift9.Sim.Match;
using UnityEngine;

namespace Shift9.Sim.Tests
{
    public sealed class PossessionSimTests
    {
        private const float Dt = SimConstants.FixedTimestep;

        private static bool InBounds(Vector3 p) =>
            Mathf.Abs(p.x) <= SimConstants.CourtHalfWidth + 0.01f &&
            Mathf.Abs(p.z) <= SimConstants.CourtHalfLength + 0.01f;

        [Test]
        public void Possession_ReachesAShotAndResolves()
        {
            var sim = new PossessionSim(seed: 7);
            PossessionPhase final = PossessionPhase.BringUp;
            for (int i = 0; i < 1200; i++)
            {
                sim.Tick(Dt);
                if (sim.Phase == PossessionPhase.Made || sim.Phase == PossessionPhase.Missed)
                {
                    final = sim.Phase;
                    break;
                }
            }
            Assert.That(final, Is.EqualTo(PossessionPhase.Made).Or.EqualTo(PossessionPhase.Missed));
        }

        [Test]
        public void Score_OnlyChangesOnAMake()
        {
            // Sweep seeds so we exercise both outcomes.
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var sim = new PossessionSim(seed);
                for (int i = 0; i < 1200 && sim.Phase != PossessionPhase.Made && sim.Phase != PossessionPhase.Missed; i++)
                    sim.Tick(Dt);

                if (sim.Phase == PossessionPhase.Made)
                    Assert.GreaterOrEqual(sim.HomeScore, 2);
                else
                    Assert.AreEqual(0, sim.HomeScore);
            }
        }

        [Test]
        public void Players_StayInBoundsThroughout()
        {
            var sim = new PossessionSim(seed: 3);
            for (int i = 0; i < 1200; i++)
            {
                sim.Tick(Dt);
                for (int p = 0; p < sim.PlayerCount; p++)
                    Assert.IsTrue(InBounds(sim.GetPlayer(p).Position), $"player {p} left the court");
                if (sim.Phase == PossessionPhase.Made || sim.Phase == PossessionPhase.Missed) break;
            }
        }

        [Test]
        public void SameSeed_ProducesIdenticalPossession()
        {
            var a = new PossessionSim(seed: 99);
            var b = new PossessionSim(seed: 99);
            for (int i = 0; i < 1200; i++)
            {
                a.Tick(Dt);
                b.Tick(Dt);
            }

            Assert.AreEqual(a.Phase, b.Phase);
            Assert.AreEqual(a.HomeScore, b.HomeScore);
            Assert.AreEqual(a.BallPosition, b.BallPosition);
            for (int p = 0; p < a.PlayerCount; p++)
                Assert.AreEqual(a.GetPlayer(p).Position, b.GetPlayer(p).Position);
        }
    }
}
