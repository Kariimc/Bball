using Shift9.Sim.Ball;
using Shift9.Sim.Core;
using Shift9.Sim.Defense;
using Shift9.Sim.Players;
using Shift9.Sim.Rules;
using Shift9.Sim.Shooting;
using UnityEngine;

namespace Shift9.Sim.Match
{
    public enum PossessionPhase : byte { BringUp, Shooting, Made, Missed }
    public enum PossessionEvent : byte { None, ShotReleased, ShotMade, ShotMissed }

    public struct SimPlayerState
    {
        public Vector3 Position;
        public Vector3 Target;
        public bool IsOffense;
        public bool IsHomeTeam;
        public float Speed;
    }

    /// <summary>
    /// A deterministic single possession for either team attacking either basket. Players walk
    /// into a half-court set, the ball handler shoots (rating + contest + timing → make/miss via
    /// <see cref="ShotResolver"/>), the ball flies and bounces (<see cref="BallBody"/> +
    /// <see cref="RimSolver"/>), and the shared scoreboard updates. Same seed ⇒ identical possession.
    /// </summary>
    public sealed class PossessionSim
    {
        public const int PlayersPerTeam = 5;
        public const int TotalPlayers = 10;

        private const float PlayerSpeed = 14f;
        private const float HoldHeight = 4f;
        private const float ReleaseHeight = 7f;
        private const float ContestRating = 72f;
        private const float ShooterRating = 80f;
        private const float ArriveRadius = 1f;
        private const float MaxBringUpSeconds = 6f;
        private const float FloorTouch = 0.6f;

        private readonly SimPlayerState[] _players = new SimPlayerState[TotalPlayers];
        private readonly BallBody _ball = new BallBody();
        private readonly RimSolver _rim;
        private readonly Scoreboard _scoreboard;
        private DeterministicRng _rng;

        private readonly bool _offenseIsHome;
        private readonly bool _attackHomeBasket;
        private readonly float _dir;            // +1 attacks +z, -1 attacks -z
        private readonly Vector3 _shotSpot;
        private readonly int _handler = 0;

        private bool _ballInFlight;
        private Vector3 _ballHeld;
        private bool _shotMade;
        private ShotZone _shotZone;
        private PossessionPhase _phase = PossessionPhase.BringUp;
        private float _phaseTime;

        public PossessionPhase Phase => _phase;
        public int HomeScore => _scoreboard.HomeScore;
        public int AwayScore => _scoreboard.AwayScore;
        public int PlayerCount => TotalPlayers;
        public SimPlayerState GetPlayer(int i) => _players[i];
        public Vector3 BallPosition => _ballInFlight ? _ball.Position : _ballHeld;

        private Vector3 Basket => _attackHomeBasket ? SimConstants.HoopHome : SimConstants.HoopAway;

        public PossessionSim(ulong seed, Scoreboard scoreboard, bool offenseIsHome, bool attackHomeBasket)
        {
            _rng = new DeterministicRng(seed);
            _scoreboard = scoreboard;
            _offenseIsHome = offenseIsHome;
            _attackHomeBasket = attackHomeBasket;
            _dir = attackHomeBasket ? -1f : 1f;
            _rim = attackHomeBasket ? RimSolver.ForHome() : RimSolver.ForAway();
            _shotSpot = new Vector3(0f, 0f, 22f * _dir);

            Vector3[] offense = Formation.Offense();
            Vector3[] defense = Formation.Defense();
            for (int i = 0; i < PlayersPerTeam; i++)
            {
                Vector3 setSpot = Mirror(offense[i]);
                _players[i] = new SimPlayerState
                {
                    Position = setSpot - new Vector3(0f, 0f, 10f * _dir),  // walk up from behind
                    Target = i == _handler ? _shotSpot : setSpot,
                    IsOffense = true,
                    IsHomeTeam = offenseIsHome,
                    Speed = PlayerSpeed
                };

                Vector3 defSpot = Mirror(defense[i]);
                _players[PlayersPerTeam + i] = new SimPlayerState
                {
                    Position = defSpot - new Vector3(0f, 0f, 8f * _dir),
                    Target = defSpot,
                    IsOffense = false,
                    IsHomeTeam = !offenseIsHome,
                    Speed = PlayerSpeed
                };
            }
            _ballHeld = _players[_handler].Position + new Vector3(0f, HoldHeight, 0f);
        }

        private Vector3 Mirror(Vector3 p) => new Vector3(p.x, p.y, p.z * _dir);

        public PossessionEvent Tick(float dt)
        {
            if (_phase == PossessionPhase.Made || _phase == PossessionPhase.Missed)
                return PossessionEvent.None;

            _phaseTime += dt;
            UpdateMovement(dt);

            if (_phase == PossessionPhase.BringUp)
            {
                Vector3 h = _players[_handler].Position;
                float dist = Vector2.Distance(new Vector2(h.x, h.z), new Vector2(_shotSpot.x, _shotSpot.z));
                if (dist <= ArriveRadius || _phaseTime >= MaxBringUpSeconds)
                {
                    ReleaseShot();
                    return PossessionEvent.ShotReleased;
                }
                return PossessionEvent.None;
            }

            BallContact contact = _ball.Step(dt, _rim);
            if (contact == BallContact.ThroughNet && _shotMade)
            {
                _scoreboard.AddBasket(_offenseIsHome, _shotZone);
                _phase = PossessionPhase.Made;
                return PossessionEvent.ShotMade;
            }
            if (_ball.Position.y <= FloorTouch)
            {
                _phase = PossessionPhase.Missed;
                return PossessionEvent.ShotMissed;
            }
            return PossessionEvent.None;
        }

        private void UpdateMovement(float dt)
        {
            for (int i = 0; i < TotalPlayers; i++)
            {
                float step = _players[i].Speed * dt;
                _players[i].Position = MoveToward(_players[i].Position, _players[i].Target, step);
            }
            if (!_ballInFlight)
                _ballHeld = _players[_handler].Position + new Vector3(0f, HoldHeight, 0f);
        }

        private void ReleaseShot()
        {
            Vector3 handler = _players[_handler].Position;

            var defenders = new DefenderState[PlayersPerTeam];
            for (int i = 0; i < PlayersPerTeam; i++)
                defenders[i] = new DefenderState(_players[PlayersPerTeam + i].Position, (byte)ContestRating);

            float openness = OpennessCalculator.Compute(handler, _attackHomeBasket, defenders);

            var ctx = new ShotContext
            {
                Position = handler,
                HomeBasket = _attackHomeBasket,
                Attributes = AttributeProfile.Uniform((byte)ShooterRating),
                Dynamics = PlayerDynamics.Default,
                Openness = openness,
                ReleaseErrorSeconds = (_rng.NextFloat() - 0.5f) * 0.06f,
                IsFreeThrow = false
            };

            ShotResult result = ShotResolver.Resolve(ctx, ref _rng, ShotModelConfig.Default);
            _shotMade = result.Made;
            _shotZone = result.Zone;

            // Make → aim at rim center (swish); miss → aim at the front rim so it clanks and falls,
            // keeping the visible flight faithful to the resolved result.
            Vector3 from = handler + new Vector3(0f, ReleaseHeight, 0f);
            Vector3 target = _shotMade
                ? Basket
                : Basket + new Vector3(0f, 0f, -_dir * (SimConstants.RimRadius + 0.1f));

            if (!ProjectileSolver.TrySolveLaunch(from, target, 50f, out Vector3 velocity, out _))
                ProjectileSolver.TrySolveLaunch(from, Basket, 50f, out velocity, out _);

            _ball.Launch(from, velocity);
            _ballInFlight = true;
            _phase = PossessionPhase.Shooting;
            _phaseTime = 0f;
        }

        private static Vector3 MoveToward(Vector3 current, Vector3 target, float maxDelta)
        {
            Vector3 delta = target - current;
            float dist = delta.magnitude;
            if (dist <= maxDelta || dist < 1e-5f) return target;
            return current + delta / dist * maxDelta;
        }
    }
}
