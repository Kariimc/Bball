using Shift9.Sim.Core;
using Shift9.Sim.Moves;
using Shift9.Sim.Players;
using Shift9.Sim.Rules;
using Shift9.Sim.Shooting;
using Shift9.Sim.Stats;
using UnityEngine;

namespace Shift9.Sim.Match
{
    /// <summary>What happened on a single game tick: the event and the player it belongs to.</summary>
    public readonly struct TickReport
    {
        public readonly PossessionEvent Event;
        public readonly int PlayerIndex; // -1 when none
        public TickReport(PossessionEvent ev, int playerIndex) { Event = ev; PlayerIndex = playerIndex; }
        public static readonly TickReport None = new TickReport(PossessionEvent.None, -1);
    }

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
        private readonly GameClock _clock;
        private readonly Scoreboard _scoreboard = new Scoreboard();
        private readonly Possession _possession = new Possession(Team.Home);
        private DeterministicRng _master;
        private PossessionSim _current;
        private readonly AttributeProfile[] _homeAttrs;
        private readonly AttributeProfile[] _awayAttrs;
        private readonly BoxScore _box = new BoxScore();

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
        public int BallHolderIndex => _current.BallHolderIndex;
        public DribbleMove LastDribbleMove => _current.LastDribbleMove;
        public FinishMove LastFinishMove => _current.LastFinishMove;
        public DefenseMove LastBlock => _current.LastBlock;
        public int LastBlockerIndex => _current.LastBlockerIndex;
        public BoxScore Box => _box;

        public GameSim(ulong seed, float quarterLength = 720f, int numQuarters = 4,
            AttributeProfile[] homeRoster = null, AttributeProfile[] awayRoster = null)
        {
            _master = new DeterministicRng(seed);
            _clock = new GameClock(quarterLength, numQuarters);
            // Each team keeps one fixed roster for the whole game (consistent players per possession).
            _homeAttrs = homeRoster ?? RandomRoster.Generate(ref _master, PossessionSim.PlayersPerTeam);
            _awayAttrs = awayRoster ?? RandomRoster.Generate(ref _master, PossessionSim.PlayersPerTeam);
            StartPossession(fullShotClock: true);
        }

        public TickReport Tick(float dt)
        {
            if (_clock.GameOver) return TickReport.None;

            ClockEvent ce = _clock.Tick(dt);
            if (ce == ClockEvent.GameEnded) return TickReport.None;
            if (ce == ClockEvent.QuarterEnded) { _clock.AdvanceQuarter(); StartPossession(true); return TickReport.None; }
            if (ce == ClockEvent.ShotClockViolation) { _possession.Flip(); StartPossession(true); return TickReport.None; }

            PossessionEvent pe = _current.Tick(dt);
            int who = _current.LastEventPlayer; // capture before a new possession replaces _current
            RecordStats(pe, who);               // record while _current/_possession still reflect this play

            if (pe == PossessionEvent.ShotMade)
            {
                _possession.Flip();
                StartPossession(true);
            }
            else if (pe == PossessionEvent.ShotMissed)
            {
                if (_current.LastShotFouled)
                {
                    _possession.Flip(); // shooter was at the line, not a live rebound
                    StartPossession(true);
                }
                else
                {
                    bool offensiveRebound = _current.LastReboundOffensive;
                    if (!offensiveRebound) _possession.Flip();
                    StartPossession(offensiveRebound ? false : true);
                }
            }
            else if (pe == PossessionEvent.Turnover)
            {
                _possession.Flip();
                StartPossession(true);
            }

            return new TickReport(pe, pe == PossessionEvent.None ? -1 : who);
        }

        // Box-score recording. Called before any possession flip, so _possession reflects who was
        // on offense for this play. Offense slots 0..4 map to the offense team, defense slots 5..9
        // to the other team.
        private void RecordStats(PossessionEvent pe, int who)
        {
            bool offHome = _possession.HomeOnOffense;
            switch (pe)
            {
                case PossessionEvent.ShotReleased:
                {
                    StatLine shooter = Line(who, offHome);
                    shooter.FieldGoalsAttempted++;
                    if (_current.LastShotZone == ShotZone.ThreePoint) shooter.ThreesAttempted++;

                    if (_current.LastBlock == DefenseMove.SignatureBlock && _current.LastBlockerIndex >= 0)
                        Line(_current.LastBlockerIndex, offHome).Blocks++;

                    if (_current.LastShotFouled)
                    {
                        if (_current.FoulingDefenderIndex >= 0) Line(_current.FoulingDefenderIndex, offHome).Fouls++;
                        shooter.FreeThrowsAttempted += _current.FreeThrowsAttempted;
                        shooter.FreeThrowsMade += _current.FreeThrowsMade;
                        shooter.Points += _current.FreeThrowsMade;
                    }
                    break;
                }
                case PossessionEvent.ShotMade:
                {
                    StatLine shooter = Line(who, offHome);
                    shooter.FieldGoalsMade++;
                    bool three = _current.LastShotZone == ShotZone.ThreePoint;
                    shooter.Points += three ? 3 : 2;
                    if (three) shooter.ThreesMade++;
                    if (_current.LastAssistPlayer >= 0) Line(_current.LastAssistPlayer, offHome).Assists++;
                    break;
                }
                case PossessionEvent.ShotMissed:
                    if (!_current.LastShotFouled) Line(who, offHome).Rebounds++;
                    break;
                case PossessionEvent.Turnover:
                    Line(who, offHome).Turnovers++;
                    if (_current.LastStealerIndex >= 0) Line(_current.LastStealerIndex, offHome).Steals++;
                    break;
            }
        }

        private StatLine Line(int slot, bool offenseIsHome)
        {
            bool home = slot < PossessionSim.PlayersPerTeam ? offenseIsHome : !offenseIsHome;
            int idx = slot < PossessionSim.PlayersPerTeam ? slot : slot - PossessionSim.PlayersPerTeam;
            return _box.Line(home, idx);
        }

        private void StartPossession(bool fullShotClock)
        {
            bool offenseIsHome = _possession.HomeOnOffense;
            bool attackHomeBasket = !offenseIsHome; // home attacks +z (away basket), away attacks -z
            ulong seed = _master.NextULong();
            AttributeProfile[] offenseAttrs = offenseIsHome ? _homeAttrs : _awayAttrs;
            AttributeProfile[] defenseAttrs = offenseIsHome ? _awayAttrs : _homeAttrs;
            _current = new PossessionSim(seed, _scoreboard, offenseIsHome, attackHomeBasket, offenseAttrs, defenseAttrs);
            _clock.ResetShotClock(fullShotClock);
        }
    }
}
