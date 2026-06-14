using NUnit.Framework;
using Shift9.Sim.Players;

namespace Shift9.Sim.Tests
{
    public sealed class AttributeFormulaTests
    {
        [Test]
        public void EliteShooter_GetsHighThreePoint()
        {
            var s = new PlayerStats { ThreePtPct = 0.44f, ThreePtAtt = 9f, FreeThrowPct = 0.9f };
            AttributeProfile a = AttributeFormula.FromStats(s);
            Assert.Greater(a.ThreePoint, 80);
            Assert.Greater(a.FreeThrow, 80);
        }

        [Test]
        public void RimProtector_GetsHighInteriorDefenseAndDunk()
        {
            var s = new PlayerStats { HeightInches = 87f, Blocks = 3f, Rebounds = 12f, WeightLbs = 240f };
            AttributeProfile a = AttributeFormula.FromStats(s);
            Assert.Greater(a.InteriorDefense, 80);
            Assert.Greater(a.DunkRating, 70);
            Assert.GreaterOrEqual(a.VerticalLeap, 65);
        }

        [Test]
        public void Playmaker_GetsHighPassingAndHandle()
        {
            var s = new PlayerStats { Assists = 10f, Turnovers = 2f, Points = 25f };
            AttributeProfile a = AttributeFormula.FromStats(s);
            Assert.Greater(a.PassingAccuracy, 75);
            Assert.Greater(a.HandleControl, 70);
        }

        [Test]
        public void RatingsAreClampedToByteRange()
        {
            var huge = new PlayerStats
            {
                Points = 100f, FieldGoalPct = 1f, ThreePtPct = 1f, ThreePtAtt = 50f, FreeThrowPct = 1f,
                Assists = 50f, Turnovers = 0f, Rebounds = 50f, OffRebounds = 30f, Blocks = 20f,
                Steals = 20f, HeightInches = 120f, WeightLbs = 400f
            };
            AttributeProfile a = AttributeFormula.FromStats(huge);
            Assert.LessOrEqual(a.ThreePoint, 99);
            Assert.LessOrEqual(a.InteriorDefense, 99);
            Assert.GreaterOrEqual(a.Speed, 0);

            var zero = new PlayerStats();
            AttributeProfile z = AttributeFormula.FromStats(zero);
            Assert.GreaterOrEqual(z.ThreePoint, 0);
            Assert.LessOrEqual(z.ThreePoint, 99);
        }
    }
}
