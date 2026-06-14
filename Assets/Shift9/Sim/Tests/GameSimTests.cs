using System.Collections.Generic;
using NUnit.Framework;
using Shift9.Sim.Core;
using Shift9.Sim.Match;
using Shift9.Sim.Rules;
using UnityEngine;

namespace Shift9.Sim.Tests
{
    public sealed class GameSimTests
    {
        private const float Dt = SimConstants.FixedTimestep;

        private static bool InBounds(Vector3 p) =>
            Mathf.Abs(p.x) <= SimConstants.CourtHalfWidth + 0.01f &&
            Mathf.Abs(p.z) <= SimConstants.CourtHalfLength + 0.01f;

        [Test]
        public void Game_RunsToFinalBuzzer()
        {
            var game = new GameSim(seed: 5, quarterLength: 24f, numQuarters: 1);
            for (int i = 0; i < 3000 && !game.GameOver; i++) game.Tick(Dt);

            Assert.IsTrue(game.GameOver);
            Assert.AreEqual(0f, game.GameTimeRemaining, 0.001f);
        }

        [Test]
        public void BothTeams_GetPossessions()
        {
            var game = new GameSim(seed: 5, quarterLength: 24f, numQuarters: 1);
            var seen = new HashSet<Team>();
            for (int i = 0; i < 3000 && !game.GameOver; i++)
            {
                seen.Add(game.OffenseTeam);
                game.Tick(Dt);
            }
            Assert.IsTrue(seen.Contains(Team.Home));
            Assert.IsTrue(seen.Contains(Team.Away));
        }

        [Test]
        public void Players_StayInBoundsAllGame()
        {
            var game = new GameSim(seed: 8, quarterLength: 24f, numQuarters: 1);
            for (int i = 0; i < 3000 && !game.GameOver; i++)
            {
                game.Tick(Dt);
                for (int p = 0; p < game.PlayerCount; p++)
                    Assert.IsTrue(InBounds(game.GetPlayer(p).Position), $"player {p} left the court");
            }
        }

        [Test]
        public void SameSeed_ProducesIdenticalGame()
        {
            var a = new GameSim(seed: 2024, quarterLength: 24f, numQuarters: 1);
            var b = new GameSim(seed: 2024, quarterLength: 24f, numQuarters: 1);
            for (int i = 0; i < 2000; i++) { a.Tick(Dt); b.Tick(Dt); }

            Assert.AreEqual(a.HomeScore, b.HomeScore);
            Assert.AreEqual(a.AwayScore, b.AwayScore);
            Assert.AreEqual(a.GameOver, b.GameOver);
            Assert.AreEqual(a.GameTimeRemaining, b.GameTimeRemaining, 0.0001f);
            Assert.AreEqual(a.BallPosition, b.BallPosition);
            for (int p = 0; p < a.PlayerCount; p++)
                Assert.AreEqual(a.GetPlayer(p).Position, b.GetPlayer(p).Position);
        }
    }
}
