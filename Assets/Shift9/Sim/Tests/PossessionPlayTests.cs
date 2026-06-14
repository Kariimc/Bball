using System.Collections.Generic;
using NUnit.Framework;
using Shift9.Sim.Core;
using Shift9.Sim.Match;
using Shift9.Sim.Rules;
using Shift9.Sim.Shooting;

namespace Shift9.Sim.Tests
{
    public sealed class PossessionPlayTests
    {
        private const float Dt = SimConstants.FixedTimestep;

        private static bool Terminal(PossessionPhase p) =>
            p == PossessionPhase.Made || p == PossessionPhase.Missed || p == PossessionPhase.Turnover;

        private static PossessionSim RunToEnd(ulong seed)
        {
            var sim = new PossessionSim(seed, new Scoreboard(), offenseIsHome: true, attackHomeBasket: false);
            for (int i = 0; i < 2000 && !Terminal(sim.Phase); i++) sim.Tick(Dt);
            return sim;
        }

        [Test]
        public void ShotLocation_VariesAcrossSeeds()
        {
            // Passing chains + drives should produce shots from different zones (incl. threes).
            var zones = new HashSet<ShotZone>();
            for (ulong s = 1; s <= 40; s++)
            {
                var sim = RunToEnd(s);
                if (sim.Phase != PossessionPhase.Turnover) zones.Add(sim.LastShotZone); // only count actual shots
            }
            Assert.GreaterOrEqual(zones.Count, 2);
            Assert.IsTrue(zones.Contains(ShotZone.ThreePoint), "expected some three-point attempts");
        }

        [Test]
        public void MakesAndMisses_BothOccur()
        {
            bool made = false, missed = false;
            for (ulong s = 1; s <= 40 && !(made && missed); s++)
            {
                PossessionPhase p = RunToEnd(s).Phase;
                if (p == PossessionPhase.Made) made = true;
                else if (p == PossessionPhase.Missed) missed = true;
            }
            Assert.IsTrue(made);
            Assert.IsTrue(missed);
        }

        [Test]
        public void Rebounds_GoToBothTeams()
        {
            bool offensive = false, defensive = false;
            for (ulong s = 1; s <= 80 && !(offensive && defensive); s++)
            {
                var sim = RunToEnd(s);
                if (sim.Phase != PossessionPhase.Missed) continue;
                if (sim.LastReboundOffensive) offensive = true;
                else defensive = true;
            }
            Assert.IsTrue(defensive, "expected at least one defensive rebound");
            Assert.IsTrue(offensive, "expected at least one offensive rebound");
        }
    }
}
