using NUnit.Framework;
using Shift9.Sim.Players;

namespace Shift9.Sim.Tests
{
    /// <summary>
    /// Calibration: realistic stat lines must land in sensible rating bands. These lock the
    /// formula's behavior against archetype reference players so future tweaks don't drift.
    /// </summary>
    public sealed class AttributeFormulaCalibrationTests
    {
        [Test]
        public void EliteScorer_HighShootingAndHandle()
        {
            var a = AttributeFormula.FromStats(new PlayerStats
            {
                Points = 30, FieldGoalPct = 0.48f, ThreePtPct = 0.38f, ThreePtAtt = 9f, FreeThrowPct = 0.90f,
                Assists = 5, Turnovers = 2, Rebounds = 6, OffRebounds = 1, Blocks = 0.5f, Steals = 1f,
                HeightInches = 79, WeightLbs = 220
            });
            Assert.That(a.ThreePoint, Is.InRange(68, 90));
            Assert.That(a.FreeThrow, Is.InRange(82, 99));
            Assert.That(a.MidRange, Is.InRange(60, 90));
        }

        [Test]
        public void RimRunningBig_StrongInsideWeakShooting()
        {
            var a = AttributeFormula.FromStats(new PlayerStats
            {
                Points = 16, FieldGoalPct = 0.65f, ThreePtPct = 0f, ThreePtAtt = 0f, FreeThrowPct = 0.60f,
                Assists = 1.5f, Turnovers = 2, Rebounds = 11, OffRebounds = 3.5f, Blocks = 2.5f, Steals = 0.5f,
                HeightInches = 83, WeightLbs = 250
            });
            Assert.That(a.InteriorDefense, Is.InRange(70, 99));
            Assert.That(a.DunkRating, Is.InRange(62, 99));
            Assert.That(a.PhysicalStrength, Is.InRange(60, 99));
            Assert.Less(a.ThreePoint, 25);
            Assert.Less(a.FreeThrow, 50);
        }

        [Test]
        public void PassFirstGuard_HighPlaymakingAndSpeed()
        {
            var a = AttributeFormula.FromStats(new PlayerStats
            {
                Points = 15, FieldGoalPct = 0.45f, ThreePtPct = 0.36f, ThreePtAtt = 5f, FreeThrowPct = 0.88f,
                Assists = 10, Turnovers = 2.5f, Rebounds = 4, OffRebounds = 0.5f, Blocks = 0.2f, Steals = 1.5f,
                HeightInches = 74, WeightLbs = 190
            });
            Assert.That(a.PassingAccuracy, Is.InRange(80, 99));
            Assert.That(a.HandleControl, Is.InRange(58, 90));
            Assert.That(a.Speed, Is.InRange(55, 90));
            Assert.Less(a.InteriorDefense, 60);
        }
    }
}
