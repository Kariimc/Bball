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
        public float Speed;
    }

    /// <summary>
    /// A deterministic single possession that ties the whole brain together: players walk into a
    /// half-court set, the ball handler takes a shot (rating + contest + timing → make/miss via
    /// <see cref="ShotResolver"/>), the ball flies and bounces (<see cref="BallBody"/> +
    /// <see cref="RimSolver"/>), and the score updates. Same seed ⇒ identical possession.
    ///
    /// The presentation layer reads <see cref="GetPlayer"/> / <see cref="BallPosition"/> each frame
    /// to drive the capsules and ball. No Unity randomness or physics is used.
    /// </summary>
    public sealed class PossessionSim
    {
        public const int PlayersPerTeam = 5;
        public const int TotalPlayers = 10;

        private const bool AttackHomeBasket = false;   // offense attacks the +z (away) basket
        private const float PlayerSpeed = 14f;         // ft/s
        private const float HoldHeight = 4f;           // ball height while dribbling
        private const float ReleaseHeight = 7f;        // ball height at shot release
        private const float ContestRating = 72f;
        private const float ShooterRating = 80;
        private const float ArriveRadius = 1f;         // handler "arrived" at the shot spot
        private const float MaxBringUpSeconds = 6f;
        private const float FloorTouch = 0.6f;         // ball deemed grounded below this height

        private static readonly Vector3 ShotSpot = new Vector3(0f, 0f, 22f);
        private static Vector3 Basket => SimConstants.HoopAway;

        private readonly SimPlayerState[] _players = new SimPlayerState[TotalPlayers];
        private readonly BallBody _ball = new BallBody();
        private readonly RimSolver _rim = RimSolver.ForAway();
        private readonly Scoreboard _scoreboard = new Scoreboard();
        private DeterministicRng _rng;

        private readonly int _handler = 0; // PG brings it up and shoots
        private bool _ballInFlight;
        private Vector3 _ballHeld;
        private bool _shotMade;
        private ShotZone _shotZone;
        private PossessionPhase _phase = PossessionPhase.BringUp;
        private float _phaseTime;

        public PossessionPhase Phase => _phase;
        public int HomeScore => _scoreboard.HomeScore;
        public Scoreboard Scoreboard => _scoreboard;
        public int PlayerCount => TotalPlayers;
        public SimPlayerState GetPlayer(int i) => _players[i];
        public Vector3 BallPosition => _ballInFlight ? _ball.Position : _ballHeld;

        public PossessionSim(ulong seed)
        {
            _rng = new DeterministicRng(seed);

            Vector3[] offense = Formation.Offense();
            Vector3[] defense = Formation.Defense();
            for (int i = 0; i < PlayersPerTeam; i++)
            {
                // Offense walks up from 10 ft back; the handler aims for the shot spot.
                _players[i] = new SimPlayerState
                {
                    Position = offense[i] - new Vector3(0f, 0f, 10f),
                    Target = i == _handler ? ShotSpot : offense[i],
                    IsOffense = true,
                    Speed = PlayerSpeed
                };
                _players[PlayersPerTeam + i] = new SimPlayerState
                {
                    Position = defense[i] - new Vector3(0f, 0f, 8f),
                    Target = defense[i],
                    IsOffense = false,
                    Speed = PlayerSpeed
                };
            }
            _ballHeld = _players[_handler].Position + new Vector3(0f, HoldHeight, 0f);
        }

        public PossessionEvent Tick(float dt)
        {
            if (_phase == PossessionPhase.Made || _phase == PossessionPhase.Missed)
                return PossessionEvent.None;

            _phaseTime += dt;
            UpdateMovement(dt);

            if (_phase == PossessionPhase.BringUp)
            {
                Vector3 handler = _players[_handler].Position;
                float distToSpot = Vector2.Distance(new Vector2(handler.x, handler.z), new Vector2(ShotSpot.x, ShotSpot.z));
                if (distToSpot <= ArriveRadius || _phaseTime >= MaxBringUpSeconds)
                {
                    ReleaseShot();
                    return PossessionEvent.ShotReleased;
                }
                return PossessionEvent.None;
            }

            // Shooting: advance the ball and watch for the outcome.
            BallContact contact = _ball.Step(dt, _rim);
            if (contact == BallContact.ThroughNet && _shotMade)
            {
                _scoreboard.AddBasket(home: true, _shotZone);
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

            float openness = OpennessCalculator.Compute(handler, AttackHomeBasket, defenders);

            var ctx = new ShotContext
            {
                Position = handler,
                HomeBasket = AttackHomeBasket,
                Attributes = AttributeProfile.Uniform((byte)ShooterRating),
                Dynamics = PlayerDynamics.Default,
                Openness = openness,
                ReleaseErrorSeconds = (_rng.NextFloat() - 0.5f) * 0.06f, // ±30 ms of timing noise
                IsFreeThrow = false
            };

            ShotResult result = ShotResolver.Resolve(ctx, ref _rng, ShotModelConfig.Default);
            _shotMade = result.Made;
            _shotZone = result.Zone;

            // A make is aimed at the rim center (clean swish); a miss is aimed at the front rim so
            // it clanks and falls — keeping the visible ball flight faithful to the resolved result.
            Vector3 from = handler + new Vector3(0f, ReleaseHeight, 0f);
            Vector3 target = _shotMade
                ? Basket
                : Basket + new Vector3(0f, 0f, -(SimConstants.RimRadius + 0.1f));

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
