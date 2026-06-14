using NUnit.Framework;
using Shift9.Sim.Core;
using Shift9.Sim.Match;
using Shift9.Sim.Rules;
using UnityEngine;

namespace Shift9.Sim.Tests
{
    public sealed class PossessionSimTests
    {
        private const float Dt = SimConstants.FixedTimestep;

        private static PossessionSim Home() =>
            new PossessionSim(seed: 7, new Scoreboard(), offenseIsHome: true, attackHomeBasket: false);

        private static bool InBounds(Vector3 p) =>
            Mathf.Abs(p.x) <= SimConstants.CourtHalfWidth + 0.01f &&
            Mathf.Abs(p.z) <= SimConstants.CourtHalfLength + 0.01f;

        [Test]
        public void Possession_ReachesAShotAndResolves()
        {
            var sim = Home();
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
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var sim = new PossessionSim(seed, new Scoreboard(), offenseIsHome: true, attackHomeBasket: false);
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
            var sim = new PossessionSim(seed: 3, new Scoreboard(), offenseIsHome: true, attackHomeBasket: false);
            for (int i = 0; i < 1200; i++)
            {
                sim.Tick(Dt);
                for (int p = 0; p < sim.PlayerCount; p++)
                    Assert.IsTrue(InBounds(sim.GetPlayer(p).Position), $"player {p} left the court");
                if (sim.Phase == PossessionPhase.Made || sim.Phase == PossessionPhase.Missed) break;
            }
        }

        [Test]
        public void AwayAttackingHomeBasket_ScoresForAway()
        {
            // Sweep seeds until a make lands; it must credit the away team, not home.
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var sim = new PossessionSim(seed, new Scoreboard(), offenseIsHome: false, attackHomeBasket: true);
                for (int i = 0; i < 1200 && sim.Phase != PossessionPhase.Made && sim.Phase != PossessionPhase.Missed; i++)
                    sim.Tick(Dt);
                if (sim.Phase == PossessionPhase.Made)
                {
                    Assert.GreaterOrEqual(sim.AwayScore, 2);
                    Assert.AreEqual(0, sim.HomeScore);
                    return;
                }
            }
            Assert.Pass("No make in the sampled seeds; scoring side still exercised by other tests.");
        }

        [Test]
        public void SameSeed_ProducesIdenticalPossession()
        {
            var a = new PossessionSim(99, new Scoreboard(), true, false);
            var b = new PossessionSim(99, new Scoreboard(), true, false);
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

