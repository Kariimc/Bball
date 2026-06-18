using NUnit.Framework;
using Shift9.Sim.Core;
using Shift9.Sim.Match;
using Shift9.Sim.Stats;

namespace Shift9.Sim.Tests
{
    public sealed class BoxScoreTests
    {
        private const float Dt = SimConstants.FixedTimestep;

        private static GameSim PlayGame(ulong seed, float quarterLength)
        {
            var g = new GameSim(seed, quarterLength, numQuarters: 1);
            for (int i = 0; i < 8000 && !g.GameOver; i++) g.Tick(Dt);
            return g;
        }

        [Test]
        public void BoxScore_PointsMatchScoreboard()
        {
            var g = PlayGame(seed: 7, quarterLength: 60f);
            Assert.AreEqual(g.HomeScore, g.Box.TeamPoints(true));
            Assert.AreEqual(g.AwayScore, g.Box.TeamPoints(false));
        }

        [Test]
        public void Fouls_ProduceFreeThrows()
        {
            var g = PlayGame(seed: 7, quarterLength: 120f);

            int fta = 0, fouls = 0;
            for (int t = 0; t < 2; t++)
                for (int i = 0; i < BoxScore.TeamSize; i++)
                {
                    StatLine line = g.Box.Line(home: t == 0, playerIndex: i);
                    fta += line.FreeThrowsAttempted;
                    fouls += line.Fouls;
                }

            Assert.Greater(fta, 0, "a full game should draw some shooting fouls");
            Assert.Greater(fouls, 0);
        }

        [Test]
        public void BoxScore_FieldGoalsMadeNeverExceedAttempts()
        {
            var g = PlayGame(seed: 3, quarterLength: 60f);
            for (int t = 0; t < 2; t++)
                for (int i = 0; i < BoxScore.TeamSize; i++)
                {
                    StatLine line = g.Box.Line(home: t == 0, playerIndex: i);
                    Assert.LessOrEqual(line.FieldGoalsMade, line.FieldGoalsAttempted);
                    Assert.LessOrEqual(line.ThreesMade, line.ThreesAttempted);
                    Assert.LessOrEqual(line.FreeThrowsMade, line.FreeThrowsAttempted);
                }
        }
    }
}
