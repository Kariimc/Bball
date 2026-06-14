using NUnit.Framework;
using Shift9.Sim.Rules;
using Shift9.Sim.Shooting;

namespace Shift9.Sim.Tests
{
    public sealed class GameClockTests
    {
        [Test]
        public void ShotClock_ViolationWhenItExpiresFirst()
        {
            var clock = new GameClock(quarterLength: 720f, shotClockFull: 24f);
            ClockEvent last = ClockEvent.None;
            for (int i = 0; i < 24; i++) last = clock.Tick(1f);
            Assert.AreEqual(ClockEvent.ShotClockViolation, last);
            Assert.AreEqual(0f, clock.ShotClockRemaining);
            Assert.Greater(clock.GameTimeRemaining, 0f);
        }

        [Test]
        public void QuarterEnds_WhenGameTimeRunsOut()
        {
            var clock = new GameClock(quarterLength: 5f, numQuarters: 4, shotClockFull: 24f);
            ClockEvent last = ClockEvent.None;
            for (int i = 0; i < 5; i++) last = clock.Tick(1f);
            Assert.AreEqual(ClockEvent.QuarterEnded, last);
            Assert.IsFalse(clock.GameOver);
        }

        [Test]
        public void GameEnds_OnLastQuarterExpiry()
        {
            var clock = new GameClock(quarterLength: 5f, numQuarters: 1, shotClockFull: 24f);
            ClockEvent last = ClockEvent.None;
            for (int i = 0; i < 5; i++) last = clock.Tick(1f);
            Assert.AreEqual(ClockEvent.GameEnded, last);
            Assert.IsTrue(clock.GameOver);
            Assert.AreEqual(ClockEvent.None, clock.Tick(1f)); // no-op once over
        }

        [Test]
        public void AdvanceQuarter_ResetsTimeAndShotClock()
        {
            var clock = new GameClock(quarterLength: 5f, numQuarters: 2, shotClockFull: 24f);
            for (int i = 0; i < 5; i++) clock.Tick(1f); // end Q1
            Assert.IsTrue(clock.AdvanceQuarter());
            Assert.AreEqual(2, clock.Quarter);
            Assert.AreEqual(5f, clock.GameTimeRemaining);
            Assert.AreEqual(24f, clock.ShotClockRemaining);
        }

        [Test]
        public void ResetShotClock_ClampsToTimeRemaining()
        {
            var clock = new GameClock(quarterLength: 720f, shotClockFull: 24f, shotClockResetShort: 14f);
            clock.ResetShotClock(false);
            Assert.AreEqual(14f, clock.ShotClockRemaining);
            clock.ResetShotClock(true);
            Assert.AreEqual(24f, clock.ShotClockRemaining);

            var ending = new GameClock(quarterLength: 5f, shotClockFull: 24f);
            ending.ResetShotClock(true);
            Assert.AreEqual(5f, ending.ShotClockRemaining); // can't exceed time left
        }
    }

    public sealed class ScoreboardTests
    {
        [Test]
        public void PointsFor_MatchesZone()
        {
            Assert.AreEqual(1, Scoreboard.PointsFor(ShotZone.FreeThrow));
            Assert.AreEqual(3, Scoreboard.PointsFor(ShotZone.ThreePoint));
            Assert.AreEqual(2, Scoreboard.PointsFor(ShotZone.MidRange));
            Assert.AreEqual(2, Scoreboard.PointsFor(ShotZone.AtRim));
        }

        [Test]
        public void AddBasket_CreditsCorrectTeam()
        {
            var board = new Scoreboard();
            board.AddBasket(home: true, ShotZone.ThreePoint);
            board.AddBasket(home: false, ShotZone.AtRim);
            board.AddBasket(home: true, ShotZone.FreeThrow);
            Assert.AreEqual(4, board.HomeScore);
            Assert.AreEqual(2, board.AwayScore);
        }
    }

    public sealed class PossessionTests
    {
        [Test]
        public void Flip_Alternates()
        {
            var p = new Possession(Team.Home);
            Assert.IsTrue(p.HomeOnOffense);
            Assert.AreEqual(Team.Away, p.Flip());
            Assert.AreEqual(Team.Home, p.Flip());
        }

        [Test]
        public void Set_OverridesOffense()
        {
            var p = new Possession(Team.Home);
            p.Set(Team.Away);
            Assert.AreEqual(Team.Away, p.Offense);
            Assert.IsFalse(p.HomeOnOffense);
        }
    }
}
