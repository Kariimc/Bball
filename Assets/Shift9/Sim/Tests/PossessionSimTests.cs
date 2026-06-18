using NUnit.Framework;
using Shift9.Sim.Core;
using Shift9.Sim.Match;
using Shift9.Sim.Players;
using Shift9.Sim.Rules;
using UnityEngine;

namespace Shift9.Sim.Tests
{
    public sealed class PossessionSimTests
    {
        private const float Dt = SimConstants.FixedTimestep;

        private static bool Terminal(PossessionPhase p) =>
            p == PossessionPhase.Made || p == PossessionPhase.Missed || p == PossessionPhase.Turnover;

        private static PossessionSim Home(ulong seed = 7) =>
            new PossessionSim(seed, new Scoreboard(), offenseIsHome: true, attackHomeBasket: false);

        private static bool InBounds(Vector3 p) =>
            Mathf.Abs(p.x) <= SimConstants.CourtHalfWidth + 0.01f &&
            Mathf.Abs(p.z) <= SimConstants.CourtHalfLength + 0.01f;

        [Test]
        public void Possession_ResolvesToATerminalOutcome()
        {
            var sim = Home();
            bool resolved = false;
            for (int i = 0; i < 1200; i++)
            {
                sim.Tick(Dt);
                if (Terminal(sim.Phase)) { resolved = true; break; }
            }
            Assert.IsTrue(resolved);
        }

        [Test]
        public void MadeFieldGoal_ScoresAtLeastTwo()
        {
            // Scores come only from made field goals (>=2) and free throws (fouls); never negative.
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var sim = Home(seed);
                for (int i = 0; i < 1200 && !Terminal(sim.Phase); i++) sim.Tick(Dt);

                if (sim.Phase == PossessionPhase.Made) Assert.GreaterOrEqual(sim.HomeScore, 2);
                Assert.GreaterOrEqual(sim.HomeScore, 0);
            }
        }

        [Test]
        public void Players_StayInBoundsThroughout()
        {
            var sim = Home(3);
            for (int i = 0; i < 1200; i++)
            {
                sim.Tick(Dt);
                for (int p = 0; p < sim.PlayerCount; p++)
                    Assert.IsTrue(InBounds(sim.GetPlayer(p).Position), $"player {p} left the court");
                if (Terminal(sim.Phase)) break;
            }
        }

        [Test]
        public void SameSeed_ProducesIdenticalPossession()
        {
            var a = new PossessionSim(99, new Scoreboard(), true, false);
            var b = new PossessionSim(99, new Scoreboard(), true, false);
            for (int i = 0; i < 1200; i++) { a.Tick(Dt); b.Tick(Dt); }

            Assert.AreEqual(a.Phase, b.Phase);
            Assert.AreEqual(a.HomeScore, b.HomeScore);
            Assert.AreEqual(a.BallPosition, b.BallPosition);
            for (int p = 0; p < a.PlayerCount; p++)
                Assert.AreEqual(a.GetPlayer(p).Position, b.GetPlayer(p).Position);
        }

        [Test]
        public void RatingsDriveShooting_EliteOutshootsScrub()
        {
            // Same situation, two rosters: a 95-across offense vs a 45-across offense, wide split.
            int eliteMakes = CountMakes(95, 40);
            int scrubMakes = CountMakes(45, 40);
            Assert.Greater(eliteMakes, scrubMakes);
        }

        private static int CountMakes(byte offenseRating, int seeds)
        {
            var off = Roster(offenseRating);
            var def = Roster(50);
            int makes = 0;
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var sim = new PossessionSim(seed, new Scoreboard(), true, false, off, def);
                for (int i = 0; i < 1200 && !Terminal(sim.Phase); i++) sim.Tick(Dt);
                if (sim.Phase == PossessionPhase.Made) makes++;
            }
            return makes;
        }

        private static AttributeProfile[] Roster(byte r)
        {
            var a = new AttributeProfile[PossessionSim.PlayersPerTeam];
            for (int i = 0; i < a.Length; i++) a[i] = AttributeProfile.Uniform(r);
            return a;
        }
    }
}
