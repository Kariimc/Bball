using System.Collections.Generic;
using Shift9.Sim.Ball;
using Shift9.Sim.Core;
using Shift9.Sim.Defense;
using Shift9.Sim.Players;
using Shift9.Sim.Rules;
using Shift9.Sim.Shooting;
using UnityEngine;

namespace Shift9.Sim.Match
{
    public enum PossessionPhase : byte { BringUp, Playmaking, Shooting, Rebound, Made, Missed, Turnover }
    public enum PossessionEvent : byte { None, ShotReleased, ShotMade, ShotMissed, Pass, Turnover }

    public struct SimPlayerState
    {
        public Vector3 Position;
        public Vector3 Target;
        public bool IsOffense;
        public bool IsHomeTeam;
        public float Speed;
        public AttributeProfile Attributes;
    }

    /// <summary>
    /// A deterministic single possession driven by player ratings: shooting (per-zone rating),
    /// contest (defender perimeter/interior D), rebounding (hustle/strength/leap), and passing
    /// (accuracy under pressure, with turnovers). Players bring it up, swing the ball, sometimes
    /// drive, then shoot; a miss is a live rebound. Same seed ⇒ identical possession.
    /// </summary>
    public sealed class PossessionSim
    {
        public const int PlayersPerTeam = 5;
        public const int TotalPlayers = 10;

        private const float PlayerSpeed = 14f;
        private const float PassSpeed = 30f;
        private const float HoldHeight = 4f;
        private const float ReleaseHeight = 7f;
        private const float ArriveRadius = 1f;
        private const float MaxBringUpSeconds = 6f;
        private const float MaxPlaySeconds = 4f;
        private const float FloorTouch = 0.6f;
        private const float DriveFraction = 0.55f;
        private const float DriveChance = 0.45f;
        private const float GoalsideStep = 3f;
        private const float OnBallPressure = 1.6f;
        private const float ReboundConverge = 0.5f;
        private const float ReboundRattle = 3f;
        private const float ReboundSkillBonus = 2f;   // ft of "reach" a 99 rebounder gains
        private const float PassTurnoverBase = 0.035f;
        private const float DriveTurnoverBase = 0.03f;

        private readonly SimPlayerState[] _players = new SimPlayerState[TotalPlayers];
        private readonly BallBody _ball = new BallBody();
        private readonly RimSolver _rim;
        private readonly Scoreboard _scoreboard;
        private DeterministicRng _rng;

        private readonly bool _offenseIsHome;
        private readonly bool _attackHomeBasket;
        private readonly float _dir;
        private readonly Vector3 _shotSpot;

        private bool _ballInFlight;
        private Vector3 _ballHeld;
        private PossessionPhase _phase = PossessionPhase.BringUp;
        private float _phaseTime;

        private int[] _passChain;
        private int _passLeg;
        private int _announcedLeg = -1;
        private float _passT;
        private int _holder;
        private int _shooter;
        private bool _drive;
        private bool _driveStarted;
        private int _lastEventPlayer = -1;

        private bool _shotMade;
        private ShotZone _shotZone;
        private Vector3 _ballGround;
        private bool _reboundOffensive;

        public PossessionPhase Phase => _phase;
        public int HomeScore => _scoreboard.HomeScore;
        public int AwayScore => _scoreboard.AwayScore;
        public int PlayerCount => TotalPlayers;
        public SimPlayerState GetPlayer(int i) => _players[i];
        public Vector3 BallPosition => _ballInFlight ? _ball.Position : _ballHeld;
        public ShotZone LastShotZone => _shotZone;
        public bool LastReboundOffensive => _reboundOffensive;
        public int LastEventPlayer => _lastEventPlayer;

        public int BallHolderIndex
        {
            get
            {
                if (_ballInFlight || _phase == PossessionPhase.Rebound || IsTerminal) return -1;
                if (_phase == PossessionPhase.Playmaking && _passChain != null && _passLeg < _passChain.Length - 1)
                    return -1;
                return _holder;
            }
        }

        private bool IsTerminal =>
            _phase == PossessionPhase.Made || _phase == PossessionPhase.Missed || _phase == PossessionPhase.Turnover;

        private Vector3 Basket => _attackHomeBasket ? SimConstants.HoopHome : SimConstants.HoopAway;
        private Vector3 BasketFloor => new Vector3(Basket.x, 0f, Basket.z);

        public PossessionSim(ulong seed, Scoreboard scoreboard, bool offenseIsHome, bool attackHomeBasket,
            AttributeProfile[] offenseAttrs = null, AttributeProfile[] defenseAttrs = null)
        {
            _rng = new DeterministicRng(seed);
            _scoreboard = scoreboard;
            _offenseIsHome = offenseIsHome;
            _attackHomeBasket = attackHomeBasket;
            _dir = attackHomeBasket ? -1f : 1f;
            _rim = attackHomeBasket ? RimSolver.ForHome() : RimSolver.ForAway();

            offenseAttrs = offenseAttrs ?? RandomRoster.Generate(ref _rng, PlayersPerTeam);
            defenseAttrs = defenseAttrs ?? RandomRoster.Generate(ref _rng, PlayersPerTeam);

            Vector3[] offense = Formation.Offense();
            Vector3[] defense = Formation.Defense();
            _shotSpot = Mirror(offense[0]);
            for (int i = 0; i < PlayersPerTeam; i++)
            {
                Vector3 set = Mirror(offense[i]);
                _players[i] = new SimPlayerState
                {
                    Position = set - new Vector3(0f, 0f, 10f * _dir),
                    Target = i == 0 ? _shotSpot : set,
                    IsOffense = true, IsHomeTeam = offenseIsHome, Speed = PlayerSpeed,
                    Attributes = offenseAttrs[i]
                };
                Vector3 def = Mirror(defense[i]);
                _players[PlayersPerTeam + i] = new SimPlayerState
                {
                    Position = def - new Vector3(0f, 0f, 8f * _dir),
                    Target = def,
                    IsOffense = false, IsHomeTeam = !offenseIsHome, Speed = PlayerSpeed,
                    Attributes = defenseAttrs[i]
                };
            }
            _holder = 0;
            _ballHeld = ChestPos(0);
        }

        private Vector3 Mirror(Vector3 p) => new Vector3(p.x, p.y, p.z * _dir);
        private Vector3 ChestPos(int i) => _players[i].Position + new Vector3(0f, HoldHeight, 0f);

        public PossessionEvent Tick(float dt)
        {
            if (IsTerminal) return PossessionEvent.None;

            _phaseTime += dt;
            switch (_phase)
            {
                case PossessionPhase.BringUp:    return TickBringUp(dt);
                case PossessionPhase.Playmaking: return TickPlaymaking(dt);
                case PossessionPhase.Shooting:   return TickShooting(dt);
                case PossessionPhase.Rebound:    return TickRebound(dt);
                default:                         return PossessionEvent.None;
            }
        }

        private PossessionEvent TickBringUp(float dt)
        {
            ReactiveDefense();
            MoveAll(dt);
            _ballHeld = ChestPos(_holder);

            if (FlatDist(_players[0].Position, _shotSpot) <= ArriveRadius || _phaseTime >= MaxBringUpSeconds)
                EnterPlaymaking();
            return PossessionEvent.None;
        }

        private void EnterPlaymaking()
        {
            int passes = _rng.Range(0, 3);
            var chain = new List<int> { 0 };
            int cur = 0;
            for (int k = 0; k < passes; k++) { int next = PickOther(cur); chain.Add(next); cur = next; }
            _passChain = chain.ToArray();
            _shooter = cur;
            _drive = _rng.NextFloat() < DriveChance;

            _passLeg = 0;
            _announcedLeg = -1;
            _passT = 0f;
            _holder = 0;
            _driveStarted = false;
            _phase = PossessionPhase.Playmaking;
            _phaseTime = 0f;
        }

        private PossessionEvent TickPlaymaking(float dt)
        {
            ReactiveDefense();
            MoveAll(dt);

            if (_passLeg < _passChain.Length - 1)
            {
                int from = _passChain[_passLeg];
                int to = _passChain[_passLeg + 1];
                if (_announcedLeg != _passLeg)
                {
                    _announcedLeg = _passLeg;
                    if (PassStolen(from)) return Turnover(from);
                    _lastEventPlayer = from;
                    return PossessionEvent.Pass;
                }
                _passT += dt / PassDuration(from, to);
                _ballHeld = Vector3.Lerp(ChestPos(from), ChestPos(to), Mathf.Clamp01(_passT));
                if (_passT >= 1f) { _passLeg++; _holder = _passChain[_passLeg]; _passT = 0f; }
                return PossessionEvent.None;
            }

            _holder = _shooter;
            if (_drive)
            {
                if (!_driveStarted)
                {
                    _driveStarted = true;
                    if (HandleLost(_shooter)) return Turnover(_shooter);
                    _players[_shooter].Target = Vector3.Lerp(Flat(_players[_shooter].Position), BasketFloor, DriveFraction);
                }
                _ballHeld = ChestPos(_shooter);
                if (FlatDist(_players[_shooter].Position, _players[_shooter].Target) <= ArriveRadius || _phaseTime >= MaxPlaySeconds)
                    return ReleaseShot(_shooter);
                return PossessionEvent.None;
            }

            _ballHeld = ChestPos(_shooter);
            return ReleaseShot(_shooter);
        }

        private PossessionEvent TickShooting(float dt)
        {
            BallContact contact = _ball.Step(dt, _rim);
            if (contact == BallContact.ThroughNet && _shotMade)
            {
                _scoreboard.AddBasket(_offenseIsHome, _shotZone);
                _phase = PossessionPhase.Made;
                _lastEventPlayer = _shooter;
                return PossessionEvent.ShotMade;
            }
            if (_ball.Position.y <= FloorTouch) { EnterRebound(); return PossessionEvent.None; }
            return PossessionEvent.None;
        }

        private void EnterRebound()
        {
            _ballInFlight = false;
            _ballGround = new Vector3(_ball.Position.x, SimConstants.BallRadius, _ball.Position.z);
            _ballHeld = _ballGround;
            Vector3 spot = new Vector3(
                Mathf.Clamp(_ballGround.x, -SimConstants.CourtHalfWidth, SimConstants.CourtHalfWidth),
                0f,
                Mathf.Clamp(_ballGround.z, -SimConstants.CourtHalfLength, SimConstants.CourtHalfLength));
            for (int i = 0; i < TotalPlayers; i++) _players[i].Target = spot;
            _phase = PossessionPhase.Rebound;
            _phaseTime = 0f;
        }

        private PossessionEvent TickRebound(float dt)
        {
            MoveAll(dt);
            if (_phaseTime < ReboundConverge) return PossessionEvent.None;

            int best = 0;
            float bestScore = float.MaxValue;
            Vector3 ball = Flat(_ballGround);
            for (int i = 0; i < TotalPlayers; i++)
            {
                float skill = ReboundSkill(_players[i].Attributes); // 0..1
                float score = FlatDist(_players[i].Position, ball)
                              - _rng.NextFloat() * ReboundRattle
                              - skill * ReboundSkillBonus;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            _reboundOffensive = _players[best].IsOffense;
            _lastEventPlayer = best;
            _phase = PossessionPhase.Missed;
            return PossessionEvent.ShotMissed;
        }

        private PossessionEvent ReleaseShot(int shooter)
        {
            Vector3 spot = _players[shooter].Position;
            ShotZone zone = ShotClassifier.Classify(spot, _attackHomeBasket).Zone;
            bool inside = zone == ShotZone.AtRim || zone == ShotZone.Close;

            var defenders = new DefenderState[PlayersPerTeam];
            for (int i = 0; i < PlayersPerTeam; i++)
            {
                AttributeProfile dp = _players[PlayersPerTeam + i].Attributes;
                byte contest = inside ? dp.InteriorDefense : dp.PerimeterDefense;
                defenders[i] = new DefenderState(_players[PlayersPerTeam + i].Position, contest);
            }
            float openness = OpennessCalculator.Compute(spot, _attackHomeBasket, defenders);

            var ctx = new ShotContext
            {
                Position = spot,
                HomeBasket = _attackHomeBasket,
                Attributes = _players[shooter].Attributes,
                Dynamics = PlayerDynamics.Default,
                Openness = openness,
                ReleaseErrorSeconds = (_rng.NextFloat() - 0.5f) * 0.06f,
                IsFreeThrow = false
            };

            ShotResult result = ShotResolver.Resolve(ctx, ref _rng, ShotModelConfig.Default);
            _shotMade = result.Made;
            _shotZone = result.Zone;

            Vector3 from = spot + new Vector3(0f, ReleaseHeight, 0f);
            Vector3 target = _shotMade
                ? Basket
                : Basket + new Vector3(0f, 0f, -_dir * (SimConstants.RimRadius + 0.1f));

            if (!ProjectileSolver.TrySolveLaunch(from, target, 50f, out Vector3 velocity, out _))
                ProjectileSolver.TrySolveLaunch(from, Basket, 50f, out velocity, out _);

            _ball.Launch(from, velocity);
            _ballInFlight = true;
            _phase = PossessionPhase.Shooting;
            _phaseTime = 0f;
            _lastEventPlayer = shooter;
            return PossessionEvent.ShotReleased;
        }

        // ---- rating-driven helpers ----

        private static float ReboundSkill(AttributeProfile a) =>
            (a.Hustle + a.PhysicalStrength + a.VerticalLeap) / 3f / 99f;

        // A pass is stolen more often with low accuracy and a defender draped on the passer.
        private bool PassStolen(int passer)
        {
            float accuracy = _players[passer].Attributes.PassingAccuracy / 99f;
            float dist = FlatDist(_players[PlayersPerTeam + passer].Position, _players[passer].Position);
            float pressure = Mathf.Clamp01((6f - dist) / 6f);
            float chance = PassTurnoverBase + (1f - accuracy) * 0.10f + pressure * 0.07f;
            return _rng.Chance(chance);
        }

        // A drive is more likely to lose the handle for poor ball-handlers.
        private bool HandleLost(int driver)
        {
            float handle = _players[driver].Attributes.HandleControl / 99f;
            float chance = DriveTurnoverBase + (1f - handle) * 0.08f;
            return _rng.Chance(chance);
        }

        private PossessionEvent Turnover(int player)
        {
            _phase = PossessionPhase.Turnover;
            _lastEventPlayer = player;
            return PossessionEvent.Turnover;
        }

        private void ReactiveDefense()
        {
            for (int j = 0; j < PlayersPerTeam; j++)
            {
                Vector3 man = _players[j].Position;
                Vector3 toBasket = BasketFloor - man; toBasket.y = 0f;
                Vector3 dir = toBasket.sqrMagnitude > 1e-4f ? toBasket.normalized : Vector3.forward;
                _players[PlayersPerTeam + j].Target = man + dir * GoalsideStep;
            }
            int onBall = PlayersPerTeam + _holder;
            Vector3 h = _players[_holder].Position;
            Vector3 tb = BasketFloor - h; tb.y = 0f;
            Vector3 d = tb.sqrMagnitude > 1e-4f ? tb.normalized : Vector3.forward;
            _players[onBall].Target = h + d * OnBallPressure;
        }

        private void MoveAll(float dt)
        {
            for (int i = 0; i < TotalPlayers; i++)
            {
                float step = _players[i].Speed * dt;
                _players[i].Position = MoveToward(_players[i].Position, _players[i].Target, step);
            }
        }

        private int PickOther(int cur)
        {
            int r = _rng.Range(0, PlayersPerTeam - 1);
            if (r >= cur) r++;
            return r;
        }

        private float PassDuration(int from, int to) =>
            Mathf.Max(0.15f, FlatDist(_players[from].Position, _players[to].Position) / PassSpeed);

        private static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);

        private static float FlatDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
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
