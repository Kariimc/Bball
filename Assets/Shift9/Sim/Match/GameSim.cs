using Shift9.Sim.Core;
using Shift9.Sim.Rules;
using UnityEngine;

namespace Shift9.Sim.Match
{
    /// <summary>
    /// A continuous deterministic game: runs possession after possession with the game clock and
    /// shot clock running, alternating offense on makes/turnovers and resolving rebounds on misses
    /// (offensive rebound keeps the ball with a short shot-clock reset; otherwise it flips). Home
    /// attacks the +z basket, away attacks -z. Same seed ⇒ identical game.
    ///
    /// The presentation layer reads the same accessors as a possession (players + ball) plus the
    /// clock and score.
    /// </summary>
    public sealed class GameSim
    {
        private const float OffensiveReboundChance = 0.30f;

        private readonly GameClock _clock;
        private readonly Scoreboard _scoreboard = new Scoreboard();
        private readonly Possession _possession = new Possession(Team.Home);
        private DeterministicRng _master;
        private PossessionSim _current;

        public int HomeScore => _scoreboard.HomeScore;
        public int AwayScore => _scoreboard.AwayScore;
        public int Quarter => _clock.Quarter;
        public float GameTimeRemaining => _clock.GameTimeRemaining;
        public float ShotClockRemaining => _clock.ShotClockRemaining;
        public bool GameOver => _clock.GameOver;
        public Team OffenseTeam => _possession.Offense;

        public int PlayerCount => _current.PlayerCount;
        public SimPlayerState GetPlayer(int i) => _current.GetPlayer(i);
        public Vector3 BallPosition => _current.BallPosition;

        public GameSim(ulong seed, float quarterLength = 720f, int numQuarters = 4)
        {
            _master = new DeterministicRng(seed);
            _clock = new GameClock(quarterLength, numQuarters);
            StartPossession(fullShotClock: true);
        }

        public void Tick(float dt)
        {
            if (_clock.GameOver) return;

            ClockEvent ce = _clock.Tick(dt);
            if (ce == ClockEvent.GameEnded) return;
            if (ce == ClockEvent.QuarterEnded) { _clock.AdvanceQuarter(); StartPossession(true); return; }
            if (ce == ClockEvent.ShotClockViolation) { _possession.Flip(); StartPossession(true); return; }

            PossessionEvent pe = _current.Tick(dt);
            if (pe == PossessionEvent.ShotMade)
            {
                _possession.Flip();
                StartPossession(true);
            }
            else if (pe == PossessionEvent.ShotMissed)
            {
                bool offensiveRebound = _master.NextFloat() < OffensiveReboundChance;
                if (!offensiveRebound) _possession.Flip();
                StartPossession(offensiveRebound ? false : true);
            }
        }

        private void StartPossession(bool fullShotClock)
        {
            bool offenseIsHome = _possession.HomeOnOffense;
            bool attackHomeBasket = !offenseIsHome; // home attacks +z (away basket), away attacks -z
            ulong seed = _master.NextULong();
            _current = new PossessionSim(seed, _scoreboard, offenseIsHome, attackHomeBasket);
            _clock.ResetShotClock(fullShotClock);
        }
    }
}
