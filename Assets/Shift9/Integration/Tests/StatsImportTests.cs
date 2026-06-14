using NUnit.Framework;
using Shift9.Customization.Mapping;
using Shift9.Integration;
using Shift9.Sim.Players;

namespace Shift9.Integration.Tests
{
    public sealed class StatsImportTests
    {
        [Test]
        public void Attributes_DerivedFromStats_WhenStatsPresent()
        {
            var p = new RuntimePlayer
            {
                Id = "shooter",
                Stats = new RuntimeStats { ThreePtPct = 0.44f, ThreePtAtt = 9f, FreeThrowPct = 0.9f }
            };
            AttributeProfile a = RosterAdapter.ResolveAttributes(p);
            Assert.Greater(a.ThreePoint, 80);
            Assert.Greater(a.FreeThrow, 80);
        }

        [Test]
        public void Attributes_UseExplicit_WhenNoStats()
        {
            var p = new RuntimePlayer { Id = "x", Attributes = new RuntimeAttributes { ThreePoint = 55 } };
            Assert.AreEqual(55, RosterAdapter.ResolveAttributes(p).ThreePoint);
        }

        [Test]
        public void Stats_TakePrecedenceOverExplicitAttributes()
        {
            var p = new RuntimePlayer
            {
                Attributes = new RuntimeAttributes { ThreePoint = 10 },
                Stats = new RuntimeStats { ThreePtPct = 0.44f, ThreePtAtt = 9f }
            };
            Assert.Greater(RosterAdapter.ResolveAttributes(p).ThreePoint, 50); // stats win
        }

        [Test]
        public void StartingFive_ResolvesPlayersAndFillsShortRosters()
        {
            var team = new RuntimeTeam();
            team.Players.Add(new RuntimePlayer { Id = "a", Attributes = new RuntimeAttributes { ThreePoint = 88 } });
            team.Players.Add(new RuntimePlayer { Id = "b", Stats = new RuntimeStats { ThreePtPct = 0.44f, ThreePtAtt = 9f } });

            AttributeProfile[] five = RosterAdapter.StartingFive(team);

            Assert.AreEqual(5, five.Length);
            Assert.AreEqual(88, five[0].ThreePoint);   // explicit
            Assert.Greater(five[1].ThreePoint, 80);    // stats-derived
            Assert.AreEqual(70, five[4].ThreePoint);   // filled
        }
    }
}
