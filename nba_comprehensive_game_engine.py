# ==============================================================================
#                      NBA_COMPREHENSIVE_GAME_ENGINE.PY
# ==============================================================================
# A deterministic, fully-playable NBA simulation engine.
#
# Architecture highlights:
#   * Single source of truth (GameState) for clock / score / fouls / possession.
#   * Seeded RNG threaded through every stochastic system -> reproducible games.
#   * Possession-level simulation: shot selection, contests, turnovers, fast
#     breaks, rebounds, putbacks, fouls, free throws, bonus, crunch-time tactics.
#   * Jump-ball tip-off, fatigue + substitutions, loose-ball scrambles.
#   * Every tick/possession emits a serializable telemetry dict; full play-by-play
#     and box scores export to plain dicts for data migration.
#
# Rendering/animation is intentionally decoupled: this module computes pure
# game-state telemetry and never draws anything.
# ==============================================================================

from __future__ import annotations
import math
import random
from dataclasses import dataclass, field
from enum import Enum, auto
from typing import Dict, List, Optional, Sequence, Tuple, Any

# ==============================================================================
# 1. STRUCTURAL GLOBAL ENUMS & CONFIGURATIONS
# ==============================================================================


class MatchPeriod(Enum):
    INTROS = auto()
    JUMP_BALL = auto()
    ACTIVE_PLAY = auto()
    HALFTIME = auto()
    POST_GAME = auto()


class BallState(Enum):
    POSSESSION_HELD = auto()
    IN_FLIGHT_SHOT_PASS = auto()
    LOOSE_ON_FLOOR = auto()
    OUT_OF_BOUNDS = auto()


class PlayState(Enum):
    HALF_COURT_SET = auto()
    FAST_BREAK_TRANSITION = auto()
    DEAD_BALL = auto()


class PhysicalAnimState(Enum):
    STAND_READY = auto()
    LOCOMOTION_RUN = auto()
    DIVE_GROUND_SCOOP = auto()
    OUT_OF_BOUNDS_SAVE = auto()
    TIED_UP_SCRUM = auto()
    JUMP_BALL_READY = auto()


class DefensivePosture(Enum):
    LAZY_CLOSEOUT = auto()
    HANDS_UP_DENIAL = auto()
    ACTIVE_LEAP_CONTEST = auto()
    CAMPING_THE_PAINT = auto()
    FULL_COURT_PRESS = auto()
    INTENTIONAL_FOUL_STANCE = auto()


class OffensiveStrategy(Enum):
    BALANCED_PACE = auto()
    HUNT_THREE_POINTERS = auto()
    REDUCE_CLOCK_ISO = auto()
    QUICK_TWO_ATTACK = auto()


class OffensivePlay(Enum):
    MOTION_FLOW_4OUT = auto()
    HORNS_PICK_AND_ROLL = auto()
    ISO_CLEAROUT = auto()
    LOOP_ZIPPER_THREE = auto()


class DefensiveScheme(Enum):
    MAN_TO_MAN_STICKY = auto()
    DROP_COVERAGE_PAINT = auto()
    SWITCH_EVERYTHING = auto()
    FULL_COURT_TRAP = auto()


class ShotZone(Enum):
    RIM = auto()          # 0 - 4 ft (dunk / layup range)
    PAINT_CLOSE = auto()  # 4 - 10 ft
    MID_RANGE = auto()    # 10 - 23.75 ft
    THREE = auto()        # 23.75 ft +


# ==============================================================================
# 2. CONFIGURATION & DIFFICULTY
# ==============================================================================


class AIDifficultyProfile:
    """Linear-interpolated AI behavior envelope driven by a single 0..1 slider."""

    def __init__(self, slider_value: float):
        self.raw_value: float = max(0.0, min(1.0, slider_value))
        self.reaction_latency: float = self._lerp(0.45, 0.06, self.raw_value)
        self.jump_ball_window: float = self._lerp(0.22, 0.04, self.raw_value)
        self.shot_timing_window: float = self._lerp(0.15, 0.04, self.raw_value)
        self.mistake_frequency: float = self._lerp(0.40, 0.02, self.raw_value)
        self.steal_intercept_radius: float = self._lerp(1.2, 2.5, self.raw_value)
        self.defensive_help_speed: float = self._lerp(3.5, 6.0, self.raw_value)

    @staticmethod
    def _lerp(start: float, end: float, weight: float) -> float:
        return start + (end - start) * weight


class GameSettingsManager:
    """Plain configuration object (deliberately NOT a singleton).

    The original design used a process-wide singleton; that made deterministic,
    isolated simulations impossible (two games would share difficulty state and
    test ordering would leak). Each engine instance now owns its own settings.
    """

    def __init__(self, ai_difficulty_slider: float = 0.5) -> None:
        self.ai_difficulty_slider: float = max(0.0, min(1.0, ai_difficulty_slider))
        self.current_profile: AIDifficultyProfile = AIDifficultyProfile(self.ai_difficulty_slider)

    def update_ai_difficulty(self, new_value: float) -> None:
        self.ai_difficulty_slider = max(0.0, min(1.0, new_value))
        self.current_profile = AIDifficultyProfile(self.ai_difficulty_slider)


# ==============================================================================
# 3. HIGH-PERFORMANCE VECTOR MATHEMATICS
# ==============================================================================


@dataclass(slots=True)
class Vector3:
    x: float = 0.0  # Width of court  (0 .. 50 ft)
    y: float = 0.0  # Vertical jump height axis
    z: float = 0.0  # Length of court (0 .. 94 ft)

    def distance_to(self, other: Vector3) -> float:
        return math.sqrt((self.x - other.x) ** 2 + (self.y - other.y) ** 2 + (self.z - other.z) ** 2)

    def planar_distance_to(self, other: Vector3) -> float:
        """Court-plane distance ignoring the vertical (y) axis."""
        return math.sqrt((self.x - other.x) ** 2 + (self.z - other.z) ** 2)

    def magnitude(self) -> float:
        return math.sqrt(self.x ** 2 + self.y ** 2 + self.z ** 2)

    def normalized(self) -> Vector3:
        mag = self.magnitude()
        if mag == 0.0:
            return Vector3()
        return Vector3(self.x / mag, self.y / mag, self.z / mag)

    def dot(self, other: Vector3) -> float:
        return self.x * other.x + self.y * other.y + self.z * other.z

    def __add__(self, other: Vector3) -> Vector3:
        return Vector3(self.x + other.x, self.y + other.y, self.z + other.z)

    def __sub__(self, other: Vector3) -> Vector3:
        return Vector3(self.x - other.x, self.y - other.y, self.z - other.z)

    def __mul__(self, scalar: float) -> Vector3:
        return Vector3(self.x * scalar, self.y * scalar, self.z * scalar)

    @staticmethod
    def distance_point_to_segment(point: Vector3, seg_a: Vector3, seg_b: Vector3) -> float:
        """True perpendicular distance from a point to the segment [a, b].

        Replaces the original approximate funnel computation, which projected a
        point along the hoop direction at the defender's *own* distance and then
        measured back -- producing geometrically meaningless values.
        """
        ab = seg_b - seg_a
        denom = ab.dot(ab)
        if denom == 0.0:
            return point.distance_to(seg_a)
        t = max(0.0, min(1.0, (point - seg_a).dot(ab) / denom))
        projection = seg_a + (ab * t)
        return point.distance_to(projection)


# ==============================================================================
# 4. PLAYBOOK, ENTITIES, BALL & STAT LINES
# ==============================================================================

# Court geometry constants (feet). Hoops live at z = 5.25 (near) and z = 88.75
# (far); for single-direction half-court math we target the near rim at z ~ 5.
COURT_WIDTH = 50.0
COURT_LENGTH = 94.0
HOOP_NEAR = Vector3(25.0, 10.0, 5.25)
THREE_POINT_RADIUS = 23.75

# Fallback ratings for any entity built without a complete stat block.
DEFAULT_STATS: Dict[str, int] = {
    "free_throw": 75, "shot_close": 75, "mid_range": 72, "three_point": 68,
    "dunk_rating": 70, "vertical_leap": 30, "speed": 76, "stamina": 80,
    "passing_accuracy": 72, "ball_handle": 72, "physical_strength": 72, "hustle": 75,
    "perimeter_defense": 72, "interior_defense": 72, "defensive_awareness": 74,
    "rebounding": 60, "steal": 60, "block": 50,
}


@dataclass
class PlayExecutionMetrics:
    name: str
    preferred_zone: str           # "PAINT", "MID_RANGE", "PERIMETER"
    turnover_risk_modifier: float
    shot_quality_bonus: float


class NBAPlayCaller:
    """Per-team playbook + strategic matchup calculator."""

    def __init__(self) -> None:
        self.offensive_playbook: Dict[OffensivePlay, PlayExecutionMetrics] = {
            OffensivePlay.MOTION_FLOW_4OUT: PlayExecutionMetrics("4-Out 1-In Motion", "PAINT", 0.9, 0.05),
            OffensivePlay.HORNS_PICK_AND_ROLL: PlayExecutionMetrics("Horns High PnR", "PAINT", 0.8, 0.08),
            OffensivePlay.ISO_CLEAROUT: PlayExecutionMetrics("Superstar Isolation", "MID_RANGE", 0.6, 0.02),
            OffensivePlay.LOOP_ZIPPER_THREE: PlayExecutionMetrics("Zipper Elevator Screen", "PERIMETER", 1.2, 0.12),
        }
        self.current_offense: OffensivePlay = OffensivePlay.HORNS_PICK_AND_ROLL
        self.current_defense: DefensiveScheme = DefensiveScheme.MAN_TO_MAN_STICKY

    def coach_call_offensive_play(self, play: OffensivePlay) -> None:
        self.current_offense = play

    def coach_adapt_defensive_scheme(self, scheme: DefensiveScheme) -> None:
        self.current_defense = scheme

    def evaluate_strategic_matchup(self, defense_scheme: Optional[DefensiveScheme] = None) -> float:
        """Shot-quality delta from offense's play vs. defense's scheme.

        ``defense_scheme`` lets a caller evaluate this offense against *another*
        team's defense (a two-team game has two playcallers); it defaults to this
        playcaller's own current defense for backward compatibility.
        """
        scheme = defense_scheme if defense_scheme is not None else self.current_defense
        zone = self.offensive_playbook[self.current_offense].preferred_zone

        if scheme == DefensiveScheme.DROP_COVERAGE_PAINT and zone == "PAINT":
            return -0.15
        if scheme == DefensiveScheme.SWITCH_EVERYTHING and self.current_offense == OffensivePlay.LOOP_ZIPPER_THREE:
            return -0.10
        if scheme == DefensiveScheme.FULL_COURT_TRAP and zone == "PERIMETER":
            return 0.06  # trap concedes clean perimeter looks once beaten
        if scheme == DefensiveScheme.MAN_TO_MAN_STICKY:
            return self.offensive_playbook[self.current_offense].shot_quality_bonus
        return 0.0


class EntityProxy:
    """Unified runtime proxy: physics transform + biometrics + live state.

    Per guardrail #1, physics transforms and athlete identity are never split
    apart -- a single proxy carries position/velocity AND ratings/animation.
    """

    def __init__(self, entity_id: str, team: str, name: str,
                 height_inches: float = 78.0, role: str = "SF",
                 stats: Optional[Dict[str, int]] = None):
        self.id: str = entity_id
        self.team: str = team
        self.name: str = name
        self.role: str = role
        self.position: Vector3 = Vector3()
        self.forward_vector: Vector3 = Vector3(0.0, 0.0, 1.0)
        self.velocity: Vector3 = Vector3()
        self.has_ball: bool = False
        self.is_shooting: bool = False

        # Identity biometrics & operational states.
        self.height_inches: float = height_inches
        self.current_posture: DefensivePosture = DefensivePosture.LAZY_CLOSEOUT
        self.current_offense: OffensiveStrategy = OffensiveStrategy.BALANCED_PACE
        self.current_anim: PhysicalAnimState = PhysicalAnimState.STAND_READY

        merged = dict(DEFAULT_STATS)
        if stats:
            merged.update(stats)
        self.stats: Dict[str, int] = merged

        # Conditioning: fatigue 0.0 (fresh) .. 1.0 (gassed).
        self.fatigue: float = 0.0

        # Availability / injury tracking. `available` gates lineup selection;
        # `games_out` is the count of *future* games missed (used by series sims).
        self.available: bool = True
        self.games_out: int = 0
        self.injury_note: str = ""

        self.animation_log: List[str] = []

    def rating(self, key: str) -> int:
        return self.stats.get(key, DEFAULT_STATS.get(key, 70))

    def effective(self, key: str) -> float:
        """Rating after fatigue erosion (athletic/shooting stats degrade tired)."""
        return self.rating(key) * (1.0 - 0.15 * self.fatigue)

    def offensive_overall(self) -> float:
        keys = ("shot_close", "mid_range", "three_point", "ball_handle",
                "passing_accuracy", "speed", "dunk_rating")
        return sum(self.rating(k) for k in keys) / len(keys)

    def defensive_overall(self) -> float:
        keys = ("perimeter_defense", "interior_defense", "defensive_awareness",
                "rebounding", "block", "steal")
        return sum(self.rating(k) for k in keys) / len(keys)

    def overall(self) -> float:
        return 0.5 * self.offensive_overall() + 0.5 * self.defensive_overall()

    def is_inside_paint(self) -> bool:
        return abs(self.position.x - 25.0) < 8.0 and 0.0 <= self.position.z <= 19.0

    def trigger_animation_override(self, state: PhysicalAnimState) -> None:
        self.current_anim = state
        self.animation_log.append(
            f"EXEC: {state.name} at court coordinate ({self.position.x:.1f}, {self.position.z:.1f})"
        )


class LiveBallObject:
    def __init__(self) -> None:
        self.position: Vector3 = Vector3(25.0, 2.0, 47.0)
        self.velocity: Vector3 = Vector3()
        self.state: BallState = BallState.LOOSE_ON_FLOOR


@dataclass
class PlayerStatLine:
    """Accumulating box-score line for a single athlete."""
    player_id: str
    name: str
    team: str
    role: str
    seconds: float = 0.0
    points: int = 0
    fgm: int = 0
    fga: int = 0
    tpm: int = 0
    tpa: int = 0
    ftm: int = 0
    fta: int = 0
    oreb: int = 0
    dreb: int = 0
    ast: int = 0
    stl: int = 0
    blk: int = 0
    tov: int = 0
    pf: int = 0

    @property
    def rebounds(self) -> int:
        return self.oreb + self.dreb

    def to_dict(self) -> Dict[str, Any]:
        return {
            "player_id": self.player_id, "name": self.name, "team": self.team,
            "role": self.role, "min": round(self.seconds / 60.0, 1),
            "pts": self.points, "fgm": self.fgm, "fga": self.fga,
            "tpm": self.tpm, "tpa": self.tpa, "ftm": self.ftm, "fta": self.fta,
            "oreb": self.oreb, "dreb": self.dreb, "reb": self.rebounds,
            "ast": self.ast, "stl": self.stl, "blk": self.blk,
            "tov": self.tov, "pf": self.pf,
        }


class Team:
    """A team: full roster, live on-court five, playcaller, and box score."""

    def __init__(self, name: str, side: str, players: List[EntityProxy]):
        self.name: str = name
        self.side: str = side  # "HOME" / "AWAY"
        self.roster: List[EntityProxy] = players
        self.play_caller: NBAPlayCaller = NBAPlayCaller()
        self.box: Dict[str, PlayerStatLine] = {
            p.id: PlayerStatLine(p.id, p.name, side, p.role) for p in players
        }
        self.on_court: List[EntityProxy] = self._select_starters()

    def _select_starters(self) -> List[EntityProxy]:
        pool = sorted((p for p in self.roster if p.available),
                      key=lambda p: p.overall(), reverse=True)
        starters: List[EntityProxy] = []
        for needed in ("PG", "SG", "SF", "PF", "C"):
            for p in pool:
                if p.role == needed and p not in starters:
                    starters.append(p)
                    break
        for p in pool:  # fill any role gaps with best available
            if len(starters) >= 5:
                break
            if p not in starters:
                starters.append(p)
        return starters[:5]

    @property
    def bench(self) -> List[EntityProxy]:
        return [p for p in self.roster if p.available and p not in self.on_court]

    def reset_for_new_game(self) -> None:
        """Clear per-game state (box, fatigue, lineup) while preserving roster
        identity and injury availability -- used between games of a series."""
        self.box = {p.id: PlayerStatLine(p.id, p.name, self.side, p.role) for p in self.roster}
        for p in self.roster:
            p.fatigue = 0.0
            p.has_ball = False
        self.on_court = self._select_starters()

    def player_with_ball_role(self) -> EntityProxy:
        """The primary initiator (best ball-handler on the floor)."""
        return max(self.on_court, key=lambda p: p.rating("ball_handle"))

    def stat(self, player: EntityProxy) -> PlayerStatLine:
        return self.box[player.id]


# ==============================================================================
# 5. GAME STATE (SINGLE SOURCE OF TRUTH) & SCENARIO STRATEGY
# ==============================================================================

REGULATION_QUARTER_SECONDS = 720.0
OVERTIME_SECONDS = 300.0
SHOT_CLOCK_SECONDS = 24.0
BONUS_FOUL_THRESHOLD = 5  # team fouls in a period that trigger the penalty


class GameState:
    """The one authoritative container for clock, score, fouls, possession."""

    def __init__(self) -> None:
        self.quarter: int = 1
        self.game_clock: float = REGULATION_QUARTER_SECONDS
        self.shot_clock: float = SHOT_CLOCK_SECONDS
        self.score: Dict[str, int] = {"HOME": 0, "AWAY": 0}
        self.team_fouls: Dict[str, int] = {"HOME": 0, "AWAY": 0}
        self.possession_side: str = "HOME"
        self.play_state: PlayState = PlayState.DEAD_BALL

    def add_points(self, side: str, points: int) -> None:
        self.score[side] += points

    def in_bonus(self, defending_side: str) -> bool:
        return self.team_fouls[defending_side] >= BONUS_FOUL_THRESHOLD

    def reset_period_fouls(self) -> None:
        self.team_fouls["HOME"] = 0
        self.team_fouls["AWAY"] = 0

    def is_tied(self) -> bool:
        return self.score["HOME"] == self.score["AWAY"]


class GameScenarioEngine:
    """Pure strategy advisor. Reads GameState; holds no duplicate clock/score."""

    def __init__(self, game_state: GameState):
        self.state: GameState = game_state

    # -- backward-compatible read/write shims (old demo set these directly) ----
    @property
    def quarter(self) -> int:
        return self.state.quarter

    @quarter.setter
    def quarter(self, value: int) -> None:
        self.state.quarter = value

    @property
    def game_clock(self) -> float:
        return self.state.game_clock

    @game_clock.setter
    def game_clock(self, value: float) -> None:
        self.state.game_clock = value

    @property
    def home_score(self) -> int:
        return self.state.score["HOME"]

    @home_score.setter
    def home_score(self, value: int) -> None:
        self.state.score["HOME"] = value

    @property
    def away_score(self) -> int:
        return self.state.score["AWAY"]

    @away_score.setter
    def away_score(self, value: int) -> None:
        self.state.score["AWAY"] = value

    # -- strategy --------------------------------------------------------------
    def _score_diff(self, team: str) -> int:
        """Positive => this team trails by that many points."""
        is_home = (team == "HOME")
        return (self.away_score - self.home_score) if is_home else (self.home_score - self.away_score)

    def calculate_desperation_index(self, team: str) -> float:
        if self.quarter < 4:
            return 0.0
        deficit = self._score_diff(team)
        if deficit <= 0:
            return 0.0
        clock_minutes = max(0.01, self.game_clock / 60.0)
        return float(deficit / (clock_minutes * 2.0))

    def derive_tactical_overrides(self, team: str) -> Tuple[OffensiveStrategy, DefensivePosture]:
        deficit = self._score_diff(team)  # >0 trailing, <0 leading

        # Leading team: protect the lead late.
        if deficit < 0:
            if self.quarter >= 4 and self.game_clock <= 120.0:
                return OffensiveStrategy.REDUCE_CLOCK_ISO, DefensivePosture.HANDS_UP_DENIAL
            return OffensiveStrategy.BALANCED_PACE, DefensivePosture.HANDS_UP_DENIAL

        # Trailing team: crunch-time aggression.
        if self.quarter >= 4 and self.game_clock <= 180.0:
            if self.game_clock <= 60.0 and deficit >= 4:
                return OffensiveStrategy.HUNT_THREE_POINTERS, DefensivePosture.INTENTIONAL_FOUL_STANCE
            if deficit >= 7:
                return OffensiveStrategy.HUNT_THREE_POINTERS, DefensivePosture.FULL_COURT_PRESS
            if deficit <= 3:
                return OffensiveStrategy.QUICK_TWO_ATTACK, DefensivePosture.ACTIVE_LEAP_CONTEST

        return OffensiveStrategy.BALANCED_PACE, DefensivePosture.LAZY_CLOSEOUT


# ==============================================================================
# 6. INTELLIGENT DEFENSIVE SYSTEMS
# ==============================================================================


class IntelligentDefensiveAI:
    def __init__(self, scenario_engine: GameScenarioEngine, play_caller: NBAPlayCaller):
        self.scenarios: GameScenarioEngine = scenario_engine
        # The *defending* team's playcaller; the orchestrator points this at the
        # correct team each possession.
        self.play_caller: NBAPlayCaller = play_caller

    def synchronize_tactical_mindset(self, defender: EntityProxy, offensive_player: EntityProxy) -> None:
        strategy_offense, strategy_defense = self.scenarios.derive_tactical_overrides(defender.team)
        defender.current_posture = strategy_defense
        offensive_player.current_offense = strategy_offense

    def calculate_shot_contest(self, defender: EntityProxy, shooter: EntityProxy, is_perimeter_shot: bool) -> float:
        if defender.current_posture == DefensivePosture.INTENTIONAL_FOUL_STANCE:
            return 0.0

        max_contest_distance = 6.0
        dist = defender.position.distance_to(shooter.position)
        if dist > max_contest_distance:
            return 0.0

        vector_to_shooter = (shooter.position - defender.position).normalized()
        facing_alignment = max(0.0, defender.forward_vector.dot(vector_to_shooter))

        def_stat = "perimeter_defense" if is_perimeter_shot else "interior_defense"
        base_rating = defender.effective(def_stat) / 100.0

        posture_mod = 1.0
        if defender.current_posture == DefensivePosture.ACTIVE_LEAP_CONTEST:
            posture_mod = 1.4
        elif defender.current_posture == DefensivePosture.HANDS_UP_DENIAL:
            posture_mod = 1.1
        elif defender.current_posture == DefensivePosture.FULL_COURT_PRESS:
            posture_mod = 1.25
        elif defender.current_posture == DefensivePosture.LAZY_CLOSEOUT:
            posture_mod = 0.65

        proximity_weight = 1.0 - (dist / max_contest_distance)
        # Scaled to a realistic FG%-point penalty: a tight, well-postured contest
        # shaves ~25-30 points off, not ~50+. Clamped so no shot is fully erased.
        raw_contest = proximity_weight * base_rating * facing_alignment * posture_mod
        return max(0.0, min(0.34, raw_contest * 0.42))

    def calculate_paint_denial_impedance(self, defender: EntityProxy, ball_handler: EntityProxy) -> Tuple[bool, float]:
        scheme_modifier = 1.35 if self.play_caller.current_defense == DefensiveScheme.DROP_COVERAGE_PAINT else 1.0
        distance = defender.position.distance_to(ball_handler.position)
        if distance > 4.5:
            return False, 0.0

        handler_to_hoop = (HOOP_NEAR - ball_handler.position).normalized()
        handler_to_defender = (defender.position - ball_handler.position).normalized()
        position_alignment = handler_to_hoop.dot(handler_to_defender)

        if position_alignment > 0.80:
            impedance_force = (defender.effective("physical_strength") / 100.0) * \
                              (defender.effective("interior_defense") / 100.0) * scheme_modifier * 1.2
            return (impedance_force > 0.55), impedance_force
        return False, 0.0

    def check_dunk_interception_funnel(self, defender: EntityProxy, ball_handler: EntityProxy,
                                       hoop_pos: Vector3) -> Tuple[bool, float]:
        handler_to_hoop = (hoop_pos - ball_handler.position).normalized()
        handler_to_defender = (defender.position - ball_handler.position).normalized()

        if handler_to_hoop.dot(handler_to_defender) > 0.70:
            # Proper perpendicular distance to the drive line (handler -> hoop).
            dist_to_line = Vector3.distance_point_to_segment(defender.position, ball_handler.position, hoop_pos)
            if dist_to_line < 3.5:
                suppression = (defender.effective("block") / 100.0) * (1.3 if defender.is_inside_paint() else 0.8)
                return True, suppression
        return False, 0.0

    def evaluate_intentional_foul_intercept(self, defender: EntityProxy, ball_handler: EntityProxy) -> Optional[Dict[str, Any]]:
        if defender.current_posture != DefensivePosture.INTENTIONAL_FOUL_STANCE:
            return None
        if defender.position.distance_to(ball_handler.position) <= 6.0:
            return {
                "event": "FOUL",
                "type": "INTENTIONAL_TAKE_FOUL (CLOCK MANAGEMENT)",
                "fouler_id": defender.id,
                "handler_id": ball_handler.id,
                "stop_clock": True,
            }
        return None


# ==============================================================================
# 7. SHOOTING, TRANSITION, SCRAMBLE & JUMP-BALL SYSTEMS
# ==============================================================================


# Affine map from a 0-100 shooting rating to a realistic uncontested FG% per
# zone: pct = base + (rating / 100) * span. Tuned to NBA norms (e.g. a 75-rated
# shooter hits ~39% from three, an 85-rated finisher ~73% at the rim).
SHOT_CURVE: Dict[str, Tuple[float, float]] = {
    "three_point": (0.22, 0.34),
    "mid_range": (0.22, 0.32),
    "shot_close": (0.33, 0.42),
    "dunk_rating": (0.48, 0.42),
}


class LiveShootingSystem:
    def __init__(self, settings_mgr: GameSettingsManager, defense_ai: IntelligentDefensiveAI, rng: random.Random):
        self.settings: GameSettingsManager = settings_mgr
        self.defense_ai: IntelligentDefensiveAI = defense_ai
        self.rng: random.Random = rng

    def evaluate_dunk_eligibility(self, player: EntityProxy, hoop_position: Vector3,
                                  primary_defender: Optional[EntityProxy] = None) -> Tuple[bool, float, str]:
        current_distance = player.position.planar_distance_to(hoop_position)
        max_launch_distance = 2.5 + (max(0.0, player.height_inches - 72.0) * 0.12) + \
            (player.rating("vertical_leap") * 0.22 * (player.rating("dunk_rating") / 100.0))
        log_msg = "CLEAR DRIVE TO RIM"

        if primary_defender:
            intercepted, suppression = self.defense_ai.check_dunk_interception_funnel(
                primary_defender, player, hoop_position)
            if intercepted:
                reduction = max_launch_distance * (suppression * 0.45)
                max_launch_distance = max(2.5, max_launch_distance - reduction)
                log_msg = f"CONTESTED RIM: launch window compressed by {reduction:.1f}ft"

        if player.rating("dunk_rating") < 40 or player.rating("vertical_leap") < 20:
            return False, 2.5, "STRUCTURAL LOCKOUT: Low dunk metrics."
        return (current_distance <= max_launch_distance), max_launch_distance, log_msg

    def calculate_shot_success(self, shooter: EntityProxy, distance_from_hoop: float,
                               release_timing_delta: float, primary_defender: Optional[EntityProxy] = None,
                               is_dunk_intent: bool = False, hoop_position: Optional[Vector3] = None,
                               matchup_modifier: float = 0.0) -> Tuple[bool, str, float]:
        """Resolve a shot. Returns (made, telemetry_text, probability)."""

        # 1. Rim drive / dunk mechanics.
        if is_dunk_intent and hoop_position:
            allowed, _, context_log = self.evaluate_dunk_eligibility(shooter, hoop_position, primary_defender)
            if not allowed:
                return False, f"REJECTED: {context_log}. Forced low-efficiency fallback.", 0.0
            contest_penalty = (self.defense_ai.calculate_shot_contest(primary_defender, shooter, False)
                               if primary_defender else 0.0)
            d_base, d_span = SHOT_CURVE["dunk_rating"]
            success_chance = max(0.10, d_base + (shooter.effective("dunk_rating") / 100.0) * d_span
                                 - contest_penalty + matchup_modifier)
            success_chance = min(0.99, success_chance)
            made = self.rng.random() < success_chance
            return made, f"DUNK | {context_log} | Contest {contest_penalty:.0%} | P {success_chance:.0%}", success_chance

        # 2. Zone resolution.
        is_perimeter = distance_from_hoop >= 10.0
        if distance_from_hoop >= THREE_POINT_RADIUS:
            rating_key, zone_desc = "three_point", "3-PT ARC"
        elif distance_from_hoop >= 10.0:
            rating_key, zone_desc = "mid_range", "MID-RANGE"
        else:
            rating_key, zone_desc = "shot_close", "PAINT/CLOSE"

        strategy_log = "STANDARD SET"
        if shooter.current_offense == OffensiveStrategy.HUNT_THREE_POINTERS:
            strategy_log = "DESPERATION: hunting 3"

        contest_factor = (self.defense_ai.calculate_shot_contest(primary_defender, shooter, is_perimeter)
                          if primary_defender else 0.0)
        profile = self.settings.current_profile
        timing_variance = profile.shot_timing_window if profile.shot_timing_window != 0.0 else 0.01
        timing_efficiency = math.exp(-((release_timing_delta / timing_variance) ** 2))

        curve_base, curve_span = SHOT_CURVE[rating_key]
        base = curve_base + (shooter.effective(rating_key) / 100.0) * curve_span
        final_probability = max(0.02, min(0.99, base * timing_efficiency - contest_factor + matchup_modifier))
        made = self.rng.random() < final_probability
        return made, f"{zone_desc} | {strategy_log} | P {final_probability:.0%}", final_probability


class FastBreakEngine:
    def __init__(self, rng: random.Random) -> None:
        self.rng: random.Random = rng
        self.current_state: PlayState = PlayState.HALF_COURT_SET

    def evaluate_live_turnover_transition(self, passer: EntityProxy, leak_out_attacker: EntityProxy,
                                          defenders: Sequence[EntityProxy]) -> Tuple[bool, str]:
        if not defenders:
            self.current_state = PlayState.FAST_BREAK_TRANSITION
            return True, f"RUNAWAY BREAK: {leak_out_attacker.name} streaks out unguarded!"

        deepest_defender_depth = min(d.position.z for d in defenders)
        pass_distance = passer.position.distance_to(leak_out_attacker.position)
        pass_accuracy = passer.effective("passing_accuracy") / 100.0
        turnover_chance = (pass_distance / 94.0) * (1.0 - pass_accuracy)
        if self.rng.random() < turnover_chance:
            self.current_state = PlayState.HALF_COURT_SET
            return False, "OUTLET PASS INTERCEPTED: transition broke down."

        if leak_out_attacker.position.z < deepest_defender_depth:
            self.current_state = PlayState.FAST_BREAK_TRANSITION
            return True, f"FAST BREAK: {passer.name} hits {leak_out_attacker.name} ahead of the defense!"
        self.current_state = PlayState.HALF_COURT_SET
        return False, "TRANSITION DENIED: defense got back."


class LooseBallScrambleSystem:
    def __init__(self, rng: random.Random) -> None:
        self.rng: random.Random = rng
        self.boundary_width_limit: float = COURT_WIDTH
        self.boundary_length_limit: float = COURT_LENGTH

    def process_hustle_scramble_tick(self, ball: LiveBallObject, players: List[EntityProxy]) -> Dict[str, Any]:
        if ball.state != BallState.LOOSE_ON_FLOOR:
            return {"event": "NO_ACTION"}
        if not players:
            return {"event": "BALL_ROLLING"}

        is_near_edge = (ball.position.x <= 1.5 or ball.position.x >= self.boundary_width_limit - 1.5 or
                        ball.position.z <= 1.5 or ball.position.z >= self.boundary_length_limit - 1.5)

        if is_near_edge:
            saver = min(players, key=lambda p: p.position.distance_to(ball.position))
            if saver.position.distance_to(ball.position) <= 5.0:
                saver.trigger_animation_override(PhysicalAnimState.OUT_OF_BOUNDS_SAVE)
                if self.rng.random() < (saver.effective("hustle") / 100.0) * 0.75:
                    ball.position = Vector3(25.0, 0.0, 47.0)
                    return {"event": "HEROIC_SAVE", "player_id": saver.id, "player": saver.name,
                            "details": "Dived to save the ball before it crossed the line."}
                ball.state = BallState.OUT_OF_BOUNDS
                return {"event": "TURN_OVER", "player_id": saver.id,
                        "details": f"{saver.name} could not corral it before the boundary."}

        scramblers = [p for p in players
                      if p.position.distance_to(ball.position) <= (6.0 * (p.effective("hustle") / 100.0))]
        if not scramblers:
            return {"event": "BALL_ROLLING"}

        if len(scramblers) >= 2 and scramblers[0].team != scramblers[1].team:
            a, b = scramblers[0], scramblers[1]
            a.trigger_animation_override(PhysicalAnimState.TIED_UP_SCRUM)
            b.trigger_animation_override(PhysicalAnimState.TIED_UP_SCRUM)
            ball.state = BallState.POSSESSION_HELD
            margin = a.rating("physical_strength") - b.rating("physical_strength")
            if abs(margin) > 15 and self.rng.random() < 0.60:
                dominant = a if margin > 0 else b
                dominant.has_ball = True
                return {"event": "SCRUM_WON", "winner_id": dominant.id, "winner": dominant.name,
                        "details": "Ripped possession away via strength."}
            return {"event": "HELD_BALL_WHISTLE", "details": "Tie-up. Referee calls a jump ball."}

        solo = scramblers[0]
        solo.trigger_animation_override(PhysicalAnimState.DIVE_GROUND_SCOOP)
        ball.state = BallState.POSSESSION_HELD
        solo.has_ball = True
        return {"event": "LOOSE_BALL_RECOVERY", "player_id": solo.id, "player": solo.name,
                "details": "Dived on the floor for the scoop recovery."}


class JumpBallEngine:
    def __init__(self, jumper_a: EntityProxy, jumper_b: EntityProxy, rng: random.Random):
        self.jumper_a = jumper_a
        self.jumper_b = jumper_b
        self.rng = rng
        self.apex_time = 0.0

    def initiate_toss(self) -> float:
        self.apex_time = self.rng.uniform(0.8, 1.2)
        return self.apex_time

    def execute_tip(self, jumper_a_input: Optional[float] = None,
                    jumper_b_input: Optional[float] = None) -> Tuple[EntityProxy, str]:
        # If no human timing inputs, both jumpers auto-time near the apex,
        # weighted by reaction/vertical so the better leaper usually wins.
        a_in = self.apex_time + self.rng.gauss(0.0, 0.12) if jumper_a_input is None else jumper_a_input
        b_in = self.apex_time + self.rng.gauss(0.0, 0.12) if jumper_b_input is None else jumper_b_input
        score_a = (self.jumper_a.rating("vertical_leap") / (abs(a_in - self.apex_time) + 0.1)) * self.rng.uniform(0.85, 1.15)
        score_b = (self.jumper_b.rating("vertical_leap") / (abs(b_in - self.apex_time) + 0.1)) * self.rng.uniform(0.85, 1.15)
        winner = self.jumper_a if score_a > score_b else self.jumper_b
        winner.has_ball = True
        return winner, f"TIP: {winner.name} controls the opening tip."


# ==============================================================================
# 8. POSSESSION ENGINE
# ==============================================================================


def _weighted_choice(rng: random.Random, items: Sequence[Any], weights: Sequence[float]) -> Any:
    total = sum(weights)
    if total <= 0:
        return rng.choice(list(items))
    r = rng.random() * total
    upto = 0.0
    for item, w in zip(items, weights):
        upto += w
        if r <= upto:
            return item
    return items[-1]


def _format_clock(seconds: float) -> str:
    seconds = max(0.0, seconds)
    return f"{int(seconds // 60)}:{int(seconds % 60):02d}"


class PossessionEngine:
    """Simulates one possession end-to-end and returns serializable telemetry."""

    def __init__(self, engine: "NBAUnifiedEngine"):
        self.e = engine

    # -- helpers ---------------------------------------------------------------
    def _pace_seconds(self, strategy: OffensiveStrategy, fast_break: bool) -> float:
        rng = self.e.rng
        if fast_break:
            return rng.uniform(3.0, 7.0)
        if strategy == OffensiveStrategy.REDUCE_CLOCK_ISO:
            return rng.uniform(17.0, 23.5)
        if strategy == OffensiveStrategy.QUICK_TWO_ATTACK:
            return rng.uniform(5.0, 11.0)
        if strategy == OffensiveStrategy.HUNT_THREE_POINTERS:
            return rng.uniform(7.0, 15.0)
        return rng.uniform(10.0, 19.0)

    def _pick_shot_zone(self, off: Team, strategy: OffensiveStrategy, fast_break: bool) -> ShotZone:
        # Base distribution: rim, paint-close, mid, three.
        w = {ShotZone.RIM: 22.0, ShotZone.PAINT_CLOSE: 20.0, ShotZone.MID_RANGE: 28.0, ShotZone.THREE: 30.0}
        pref = off.play_caller.offensive_playbook[off.play_caller.current_offense].preferred_zone
        if pref == "PAINT":
            w[ShotZone.RIM] += 14; w[ShotZone.PAINT_CLOSE] += 8; w[ShotZone.THREE] -= 8
        elif pref == "PERIMETER":
            w[ShotZone.THREE] += 22; w[ShotZone.MID_RANGE] -= 6
        elif pref == "MID_RANGE":
            w[ShotZone.MID_RANGE] += 16
        if strategy == OffensiveStrategy.HUNT_THREE_POINTERS:
            w[ShotZone.THREE] += 40; w[ShotZone.MID_RANGE] -= 12; w[ShotZone.PAINT_CLOSE] -= 8
        elif strategy == OffensiveStrategy.QUICK_TWO_ATTACK:
            w[ShotZone.RIM] += 24; w[ShotZone.MID_RANGE] += 8; w[ShotZone.THREE] -= 20
        if fast_break:
            w[ShotZone.RIM] += 45; w[ShotZone.THREE] += 6; w[ShotZone.MID_RANGE] -= 15; w[ShotZone.PAINT_CLOSE] -= 5
        zones = list(w.keys())
        weights = [max(1.0, w[z]) for z in zones]
        return _weighted_choice(self.e.rng, zones, weights)

    def _zone_distance(self, zone: ShotZone) -> float:
        rng = self.e.rng
        if zone == ShotZone.RIM:
            return rng.uniform(0.0, 4.0)
        if zone == ShotZone.PAINT_CLOSE:
            return rng.uniform(4.0, 10.0)
        if zone == ShotZone.MID_RANGE:
            return rng.uniform(10.0, 22.0)
        return rng.uniform(THREE_POINT_RADIUS, 27.0)

    def _pick_shooter(self, off: Team, zone: ShotZone) -> EntityProxy:
        key = {
            ShotZone.RIM: "dunk_rating",
            ShotZone.PAINT_CLOSE: "shot_close",
            ShotZone.MID_RANGE: "mid_range",
            ShotZone.THREE: "three_point",
        }[zone]
        weights = [max(1.0, p.rating(key) ** 2.2 / 1000.0) for p in off.on_court]
        return _weighted_choice(self.e.rng, off.on_court, weights)

    def _nearest_defender(self, defense: Team, shooter: EntityProxy) -> EntityProxy:
        # Role-match the primary defender; fall back to best on-ball defender.
        same_role = [d for d in defense.on_court if d.role == shooter.role]
        if same_role:
            return same_role[0]
        return max(defense.on_court, key=lambda d: d.rating("perimeter_defense"))

    def _place_shot_geometry(self, shooter: EntityProxy, defender: EntityProxy, distance: float, contest: bool) -> None:
        # Put the shooter `distance` ft from the rim along center line, and the
        # defender within (or outside) contest range so the contest math is real.
        shooter.position = Vector3(25.0, 0.0, HOOP_NEAR.z + distance)
        gap = self.e.rng.uniform(1.0, 3.0) if contest else self.e.rng.uniform(6.5, 9.0)
        defender.position = Vector3(25.0, 0.0, max(0.0, shooter.position.z - gap))
        defender.forward_vector = (shooter.position - defender.position).normalized()

    def _free_throws(self, off: Team, shooter: EntityProxy, count: int, telemetry: Dict[str, Any]) -> int:
        made = 0
        line = off.stat(shooter)
        for _ in range(count):
            line.fta += 1
            if self.e.rng.random() < shooter.effective("free_throw") / 100.0:
                line.ftm += 1
                line.points += 1
                self.e.game_state.add_points(off.side, 1)
                made += 1
        telemetry["free_throws"] = {"shooter": shooter.name, "made": made, "attempted": count}
        return made

    def _rebound(self, off: Team, defense: Team) -> Tuple[Team, EntityProxy]:
        def weight(p: EntityProxy, offensive: bool) -> float:
            base = p.rating("rebounding") + 0.4 * (p.height_inches - 72.0) + 0.3 * p.rating("hustle")
            return max(1.0, base * (0.42 if offensive else 1.0))  # defense favored
        candidates = [(off, p, True) for p in off.on_court] + [(defense, p, False) for p in defense.on_court]
        weights = [weight(p, is_off) for (_, p, is_off) in candidates]
        chosen = _weighted_choice(self.e.rng, candidates, weights)
        return chosen[0], chosen[1]

    # -- main ------------------------------------------------------------------
    def simulate_possession(self, off: Team, defense: Team, fast_break: bool = False) -> Dict[str, Any]:
        gs = self.e.game_state
        rng = self.e.rng

        # Point the defensive AI at the defending team's scheme this possession.
        self.e.defense_ai.play_caller = defense.play_caller

        handler = off.player_with_ball_role()
        primary_defender = self._nearest_defender(defense, handler)
        self.e.defense_ai.synchronize_tactical_mindset(primary_defender, handler)
        strategy = handler.current_offense

        telemetry: Dict[str, Any] = {
            "quarter": gs.quarter,
            "clock": _format_clock(gs.game_clock),
            "offense": off.name,
            "defense": defense.name,
            "fast_break": fast_break,
            "events": [],
        }
        time_elapsed = self._pace_seconds(strategy, fast_break)

        # 1. Crunch-time intentional foul (defense fouls to stop the clock).
        handler.position = Vector3(25.0, 0.0, 30.0)
        primary_defender.position = Vector3(25.0, 0.0, 28.0)
        foul_call = self.e.defense_ai.evaluate_intentional_foul_intercept(primary_defender, handler)
        if foul_call:
            gs.team_fouls[defense.side] += 1
            defense.stat(primary_defender).pf += 1
            telemetry["events"].append(foul_call["type"])
            made = self._free_throws(off, handler, 2, telemetry)
            telemetry["result"] = "INTENTIONAL_FOUL"
            telemetry["points"] = made
            telemetry["change_of_possession"] = True
            telemetry["time_elapsed"] = min(time_elapsed, 4.0)
            telemetry["score"] = dict(gs.score)
            return telemetry

        # 2. Turnover check (ball security vs. pressure).
        press = 1.6 if defense.play_caller.current_defense == DefensiveScheme.FULL_COURT_TRAP else 1.0
        to_chance = max(0.02, (0.12 - handler.effective("ball_handle") / 1000.0)
                        + primary_defender.effective("steal") / 1500.0) * press
        if rng.random() < to_chance:
            off.stat(handler).tov += 1
            stole = rng.random() < 0.55
            detail = "Live-ball steal!" if stole else "Errant pass out of bounds."
            if stole:
                thief = max(defense.on_court, key=lambda d: d.rating("steal"))
                defense.stat(thief).stl += 1
                telemetry["events"].append(f"STEAL by {thief.name}")
                # Possible fast break the other way next possession.
                leak = max(defense.on_court, key=lambda d: d.rating("speed"))
                leak.position = Vector3(25.0, 0.0, 70.0)
                thief.position = Vector3(25.0, 0.0, 30.0)
                broke, fb_text = self.e.transition.evaluate_live_turnover_transition(
                    thief, leak, [p for p in off.on_court if p is not handler])
                telemetry["events"].append(fb_text)
                telemetry["fast_break_for"] = defense.side if broke else None
            else:
                telemetry["fast_break_for"] = None
            telemetry["result"] = "TURNOVER"
            telemetry["detail"] = detail
            telemetry["points"] = 0
            telemetry["change_of_possession"] = True
            telemetry["time_elapsed"] = time_elapsed
            telemetry["score"] = dict(gs.score)
            return telemetry

        # 3. Shot selection & geometry.
        zone = self._pick_shot_zone(off, strategy, fast_break)
        distance = self._zone_distance(zone)
        shooter = handler if (strategy == OffensiveStrategy.REDUCE_CLOCK_ISO or
                              off.play_caller.current_offense == OffensivePlay.ISO_CLEAROUT) else self._pick_shooter(off, zone)
        shot_defender = self._nearest_defender(defense, shooter)
        self.e.defense_ai.synchronize_tactical_mindset(shot_defender, shooter)
        contested = rng.random() < 0.7 and not fast_break
        self._place_shot_geometry(shooter, shot_defender, distance, contested)

        is_dunk = (zone == ShotZone.RIM and shooter.rating("dunk_rating") >= 60
                   and (fast_break or rng.random() < 0.5))
        matchup_mod = off.play_caller.evaluate_strategic_matchup(defense.play_caller.current_defense)
        matchup_mod -= 0.10 * shooter.fatigue  # tired legs
        if off.side == "HOME":
            matchup_mod += self.e.home_court_edge  # home-court effect
        timing_delta = abs(rng.gauss(0.0, self.e.settings.current_profile.shot_timing_window
                                     * (0.5 if contested else 0.25)))

        shooter.is_shooting = True
        made, shot_log, prob = self.e.shooting_system.calculate_shot_success(
            shooter=shooter, distance_from_hoop=distance, release_timing_delta=timing_delta,
            primary_defender=shot_defender, is_dunk_intent=is_dunk,
            hoop_position=HOOP_NEAR if is_dunk else None, matchup_modifier=matchup_mod)
        shooter.is_shooting = False

        is_three = zone == ShotZone.THREE
        line = off.stat(shooter)
        line.fga += 1
        if is_three:
            line.tpa += 1
        telemetry["shot"] = {"shooter": shooter.name, "zone": zone.name,
                             "distance_ft": round(distance, 1), "is_dunk": is_dunk,
                             "contested": contested, "probability": round(prob, 3), "log": shot_log}

        # 4. Shooting foul? (more likely close, on contests).
        foul_base = {ShotZone.RIM: 0.16, ShotZone.PAINT_CLOSE: 0.11,
                     ShotZone.MID_RANGE: 0.05, ShotZone.THREE: 0.035}[zone]
        if contested:
            foul_base += 0.05
        shooting_foul = rng.random() < foul_base

        # 5. Block check on rim/close misses against a strong rim protector.
        blocked = False
        if not made and zone in (ShotZone.RIM, ShotZone.PAINT_CLOSE):
            blk_chance = shot_defender.effective("block") / 100.0 * (0.30 if contested else 0.12)
            if rng.random() < blk_chance:
                blocked = True
                defense.stat(shot_defender).blk += 1
                telemetry["events"].append(f"BLOCK by {shot_defender.name}")

        points = 0
        change = True
        if made:
            line.fgm += 1
            points = 3 if is_three else 2
            if is_three:
                line.tpm += 1
            line.points += points
            gs.add_points(off.side, points)
            telemetry["events"].append(f"{shooter.name} scores {points}")
            # Assist attribution.
            iso = off.play_caller.current_offense == OffensivePlay.ISO_CLEAROUT
            assist_rate = 0.22 if iso else (0.70 if zone in (ShotZone.THREE, ShotZone.MID_RANGE) else 0.55)
            mates = [p for p in off.on_court if p is not shooter]
            if mates and rng.random() < assist_rate:
                passer = _weighted_choice(rng, mates, [p.rating("passing_accuracy") for p in mates])
                off.stat(passer).ast += 1
                telemetry["events"].append(f"assist {passer.name}")
            # And-1.
            if shooting_foul:
                gs.team_fouls[defense.side] += 1
                defense.stat(shot_defender).pf += 1
                points += self._free_throws(off, shooter, 1, telemetry)
                telemetry["events"].append("AND-1")
            telemetry["result"] = "MADE_SHOT"
        elif shooting_foul and not blocked:
            gs.team_fouls[defense.side] += 1
            defense.stat(shot_defender).pf += 1
            ft_count = 3 if is_three else 2
            points = self._free_throws(off, shooter, ft_count, telemetry)
            # FGA charged above is wrong for a non-shot foul, but here the foul
            # is ON the shot attempt, so the FGA stands as a foul-drawing attempt;
            # de-charge to reflect "shooting foul, no FG attempt counted".
            line.fga -= 1
            if is_three:
                line.tpa -= 1
            telemetry["result"] = "SHOOTING_FOUL"
        else:
            # Miss -> rebound battle.
            reb_team, rebounder = self._rebound(off, defense)
            if reb_team is off:
                off.stat(rebounder).oreb += 1
                telemetry["events"].append(f"offensive rebound {rebounder.name}")
                # Immediate putback attempt (second chance).
                if rng.random() < 0.55:
                    pb_def = self._nearest_defender(defense, rebounder)
                    self._place_shot_geometry(rebounder, pb_def, 2.5, True)
                    pb_made, _, _ = self.e.shooting_system.calculate_shot_success(
                        rebounder, 2.5, abs(rng.gauss(0, 0.06)), pb_def)
                    pb_line = off.stat(rebounder)
                    pb_line.fga += 1
                    if pb_made:
                        pb_line.fgm += 1
                        pb_line.points += 2
                        gs.add_points(off.side, 2)
                        points = 2
                        telemetry["events"].append(f"PUTBACK {rebounder.name} +2")
                        telemetry["result"] = "SECOND_CHANCE_SCORE"
                    else:
                        telemetry["result"] = "MISS_OREB_NO_SCORE"
                else:
                    telemetry["result"] = "OFFENSIVE_REBOUND"
                change = True  # bound recursion: possession ends after one putback
            else:
                defense.stat(rebounder).dreb += 1
                telemetry["events"].append(f"defensive rebound {rebounder.name}")
                telemetry["result"] = "DEFENSIVE_REBOUND"
                change = True

        telemetry["points"] = points
        telemetry["change_of_possession"] = change
        telemetry["time_elapsed"] = time_elapsed
        telemetry["score"] = dict(gs.score)
        telemetry["fast_break_for"] = telemetry.get("fast_break_for")
        return telemetry


# ==============================================================================
# 9. RULES & ORCHESTRATION
# ==============================================================================


class NBARuleEngine:
    """Thin rules helper retained for API compatibility. Authoritative clock and
    score live in GameState; this exposes violation/dead-ball flags only."""

    def __init__(self, game_state: GameState):
        self.state: GameState = game_state
        self.is_dead_ball: bool = True

    def trigger_violation(self, name: str) -> Dict[str, Any]:
        self.is_dead_ball = True
        return {"event": "VIOLATION", "type": name, "turnover": True}


class NBAUnifiedEngine:
    """Top-level orchestrator: owns all systems and simulates full games."""

    def __init__(self, home: Optional[Team] = None, away: Optional[Team] = None,
                 seed: Optional[int] = None, difficulty: float = 0.5,
                 injury_rate: float = 0.0005, home_court_edge: float = 0.02):
        self.rng: random.Random = random.Random(seed)
        self.settings: GameSettingsManager = GameSettingsManager(difficulty)
        self.injury_rate: float = max(0.0, injury_rate)
        # Per-shot probability bonus for the home offense (~NBA home-court effect).
        self.home_court_edge: float = home_court_edge
        self.game_state: GameState = GameState()
        self.scenarios: GameScenarioEngine = GameScenarioEngine(self.game_state)
        self.play_caller: NBAPlayCaller = NBAPlayCaller()  # shared default (demo compat)
        self.defense_ai: IntelligentDefensiveAI = IntelligentDefensiveAI(self.scenarios, self.play_caller)
        self.shooting_system: LiveShootingSystem = LiveShootingSystem(self.settings, self.defense_ai, self.rng)
        self.transition: FastBreakEngine = FastBreakEngine(self.rng)
        self.scramble: LooseBallScrambleSystem = LooseBallScrambleSystem(self.rng)
        self.rules_engine: NBARuleEngine = NBARuleEngine(self.game_state)
        self.possession_engine: PossessionEngine = PossessionEngine(self)

        self.home: Optional[Team] = home
        self.away: Optional[Team] = away
        self.play_by_play: List[Dict[str, Any]] = []
        self.injuries: List[Dict[str, Any]] = []

        # Stepping state (populated by start_game()).
        self._teams: Dict[str, Team] = {}
        self._period: int = 0
        self._pending_fast_break: Optional[str] = None
        self._second_half_first: str = "AWAY"
        self._game_over: bool = False

    # -- conditioning ----------------------------------------------------------
    def _apply_fatigue(self, on_court: Sequence[EntityProxy], bench: Sequence[EntityProxy],
                       team: Team, seconds: float) -> None:
        for p in on_court:
            drain = (seconds / 48.0) * (1.0 - 0.4 * (p.rating("stamina") / 100.0))
            p.fatigue = min(1.0, p.fatigue + drain)
            team.stat(p).seconds += seconds
        for p in bench:
            p.fatigue = max(0.0, p.fatigue - (seconds / 36.0))

    def _substitute(self, team: Team) -> None:
        for i, starter in enumerate(list(team.on_court)):
            if starter.fatigue > 0.72:
                pool = [b for b in team.bench if b.fatigue < 0.45]
                same = [b for b in pool if b.role == starter.role]
                pick = (min(same, key=lambda b: b.fatigue) if same else
                        (min(pool, key=lambda b: b.fatigue) if pool else None))
                if pick is not None:
                    team.on_court[i] = pick

    # -- game loop -------------------------------------------------------------
    def _tip_off(self) -> str:
        center_home = max(self.home.on_court, key=lambda p: p.height_inches)
        center_away = max(self.away.on_court, key=lambda p: p.height_inches)
        jb = JumpBallEngine(center_home, center_away, self.rng)
        jb.initiate_toss()
        winner, text = jb.execute_tip()
        side = self.home.side if winner in self.home.on_court else self.away.side
        self.play_by_play.append({"quarter": 1, "event": "JUMP_BALL", "detail": text, "winner": winner.name})
        return side

    def _begin_period(self, period: int) -> None:
        gs = self.game_state
        gs.quarter = period
        gs.game_clock = OVERTIME_SECONDS if period > 4 else REGULATION_QUARTER_SECONDS
        gs.reset_period_fouls()
        # Possession arrow flips to start Q3 (loser of the opening tip).
        if period == 3:
            gs.possession_side = self._second_half_first

    def start_game(self) -> Dict[str, Any]:
        """Initialize a steppable game (tip-off + first period). Returns the
        opening telemetry. Pair with repeated step_possession() calls."""
        if self.home is None or self.away is None:
            raise ValueError("start_game requires both home and away teams.")
        self._teams = {"HOME": self.home, "AWAY": self.away}
        self.game_state.possession_side = self._tip_off()
        self._second_half_first = "AWAY" if self.game_state.possession_side == "HOME" else "HOME"
        self._period = 1
        self._pending_fast_break = None
        self._game_over = False
        self._begin_period(1)
        return {"event": "TIP_OFF", "quarter": 1,
                "possession": self.game_state.possession_side, "game_over": False}

    def step_possession(self) -> Dict[str, Any]:
        """Advance exactly one possession (rolling periods/OT as needed) and
        return its telemetry. When the game is decided, returns a GAME_OVER
        payload carrying the final summary."""
        gs = self.game_state
        if self._game_over:
            return {"event": "GAME_OVER", "game_over": True, "summary": self.build_game_summary()}

        # Period rollover / end-of-game gate.
        if gs.game_clock <= 0:
            if self._period >= 4 and not gs.is_tied():
                self._game_over = True
                return {"event": "GAME_OVER", "game_over": True, "summary": self.build_game_summary()}
            self._period += 1
            self._begin_period(self._period)

        off = self._teams[gs.possession_side]
        defense = self._teams["AWAY" if gs.possession_side == "HOME" else "HOME"]

        # Forfeit guard: a team with no eligible players cannot continue (only
        # reachable under extreme injury rates; realistic rates never empty a roster).
        if not off.on_court or not defense.on_court:
            self._game_over = True
            summary = self.build_game_summary()
            summary["forfeit"] = True
            return {"event": "GAME_OVER", "game_over": True,
                    "reason": "insufficient_players", "summary": summary}

        fb = self._pending_fast_break == off.side
        self._pending_fast_break = None

        result = self.possession_engine.simulate_possession(off, defense, fast_break=fb)
        gs.game_clock = max(0.0, gs.game_clock - result["time_elapsed"])

        self._apply_fatigue(off.on_court, off.bench, off, result["time_elapsed"])
        self._apply_fatigue(defense.on_court, defense.bench, defense, result["time_elapsed"])
        self._substitute(off)
        self._substitute(defense)

        injuries = self._check_injuries((off, defense))
        if injuries:
            result.setdefault("events", []).extend(i["event_text"] for i in injuries)
            result["new_injuries"] = injuries

        self.play_by_play.append(result)
        if result.get("fast_break_for"):
            self._pending_fast_break = result["fast_break_for"]
        if result.get("change_of_possession", True):
            gs.possession_side = "AWAY" if gs.possession_side == "HOME" else "HOME"
        result["game_over"] = False
        return result

    def simulate_game(self, verbose: bool = False) -> Dict[str, Any]:
        """Run a full game to completion by driving the stepper."""
        self.start_game()
        while True:
            result = self.step_possession()
            if result.get("game_over"):
                return result["summary"]
            if verbose:
                self._print_play(result)

    # -- injuries --------------------------------------------------------------
    _INJURY_NOTES = ("rolled ankle", "tweaked hamstring", "jammed finger",
                     "lower-back tightness", "knee soreness", "shoulder strain",
                     "hip pointer", "calf cramp")

    def _check_injuries(self, teams: Sequence[Team]) -> List[Dict[str, Any]]:
        if self.injury_rate <= 0.0:
            return []
        out: List[Dict[str, Any]] = []
        for team in teams:
            for p in list(team.on_court):
                if not p.available:
                    continue
                # Risk rises with fatigue, falls with conditioning (stamina).
                chance = self.injury_rate * (0.3 + p.fatigue) * (1.0 - 0.3 * (p.rating("stamina") / 100.0))
                if self.rng.random() < chance:
                    out.append(self._injure(team, p))
        return out

    def _injure(self, team: Team, player: EntityProxy) -> Dict[str, Any]:
        gs = self.game_state
        player.available = False
        r = self.rng.random()
        games_out = 0 if r < 0.70 else (1 if r < 0.90 else self.rng.randint(2, 5))
        player.games_out = games_out
        note = self.rng.choice(self._INJURY_NOTES)
        player.injury_note = note
        self._force_sub(team, player)
        rec = {"type": "INJURY", "player_id": player.id, "player": player.name,
               "team": team.name, "quarter": gs.quarter, "clock": _format_clock(gs.game_clock),
               "note": note, "games_out": games_out,
               "event_text": f"INJURY: {player.name} ({note}) -- out"}
        self.injuries.append(rec)
        return rec

    def _force_sub(self, team: Team, injured: EntityProxy) -> None:
        if injured not in team.on_court:
            return
        idx = team.on_court.index(injured)
        pool = [b for b in team.roster if b.available and b not in team.on_court]
        if not pool:
            team.on_court.pop(idx)  # no healthy reserves: play short-handed
            return
        same = [b for b in pool if b.role == injured.role]
        pick = (min(same, key=lambda b: b.fatigue) if same else min(pool, key=lambda b: b.fatigue))
        team.on_court[idx] = pick

    def _print_play(self, r: Dict[str, Any]) -> None:
        if r.get("result") in (None, "DEFENSIVE_REBOUND", "MISS_OREB_NO_SCORE"):
            return
        clock = r.get("clock", "")
        q = r.get("quarter", "")
        score = r.get("score", {})
        events = "; ".join(r.get("events", [])) or r.get("result", "")
        print(f"  Q{q} {clock} [{score.get('HOME',0)}-{score.get('AWAY',0)}] {r.get('offense','')}: {events}")

    # -- reporting -------------------------------------------------------------
    def build_game_summary(self) -> Dict[str, Any]:
        gs = self.game_state

        def team_block(team: Team) -> Dict[str, Any]:
            lines = sorted((s.to_dict() for s in team.box.values()),
                           key=lambda d: d["pts"], reverse=True)
            totals = {k: sum(line[k] for line in lines)
                      for k in ("pts", "fgm", "fga", "tpm", "tpa", "ftm", "fta",
                                "oreb", "dreb", "reb", "ast", "stl", "blk", "tov", "pf")}
            return {"name": team.name, "side": team.side, "score": gs.score[team.side],
                    "players": lines, "totals": totals}

        return {
            "final_score": dict(gs.score),
            "periods_played": gs.quarter,
            "winner": (self.home.name if gs.score["HOME"] > gs.score["AWAY"]
                       else self.away.name if gs.score["AWAY"] > gs.score["HOME"] else "TIE"),
            "home": team_block(self.home),
            "away": team_block(self.away),
            "possessions": len(self.play_by_play),
            "injuries": list(self.injuries),
        }

    # -- single-tick demo (preserved & upgraded to return telemetry) -----------
    def execute_game_tick(self, home_player: EntityProxy, away_player: EntityProxy,
                          defensive_squad: List[EntityProxy]) -> Dict[str, Any]:
        """Single interaction tick returning a serializable telemetry dict
        (guardrail #3 -- ticks never just print)."""
        primary_defender = defensive_squad[0]
        self.defense_ai.play_caller = self.play_caller
        telemetry: Dict[str, Any] = {"events": []}

        blocked, force = self.defense_ai.calculate_paint_denial_impedance(primary_defender, home_player)
        if blocked:
            self.play_caller.coach_call_offensive_play(OffensivePlay.LOOP_ZIPPER_THREE)
            telemetry["events"].append({"type": "PAINT_DENIAL", "defender": primary_defender.name,
                                        "force": round(force, 2), "audible": "LOOP_ZIPPER_THREE"})
            telemetry["result"] = "DRIVE_DENIED_AUDIBLE"
            return telemetry

        foul_call = self.defense_ai.evaluate_intentional_foul_intercept(primary_defender, home_player)
        if foul_call:
            telemetry["events"].append(foul_call)
            telemetry["result"] = "INTENTIONAL_FOUL"
            return telemetry

        made, log, prob = self.shooting_system.calculate_shot_success(
            shooter=home_player, distance_from_hoop=home_player.position.planar_distance_to(HOOP_NEAR),
            release_timing_delta=0.012, primary_defender=away_player)
        telemetry["events"].append({"type": "SHOT", "made": made, "probability": round(prob, 3), "log": log})
        telemetry["result"] = "MADE" if made else "MISS"
        return telemetry


# ==============================================================================
# 10. TEAM BUILDER & EXECUTION PROOF RUNNER
# ==============================================================================


def build_team(name: str, side: str, roster_data: Sequence[Dict[str, Any]]) -> Team:
    """Construct a Team of EntityProxy athletes from plain roster dictionaries."""
    players: List[EntityProxy] = []
    for i, pdata in enumerate(roster_data):
        players.append(EntityProxy(
            entity_id=f"{side[0]}{i:02d}",
            team=side,
            name=pdata["name"],
            height_inches=float(pdata.get("height_inches", 78.0)),
            role=pdata.get("role", "SF"),
            stats=pdata.get("stats"),
        ))
    return Team(name, side, players)


def _print_box(summary: Dict[str, Any]) -> None:
    print("\n" + "=" * 78)
    fs = summary["final_score"]
    print(f"FINAL  {summary['away']['name']} {fs['AWAY']}  @  {summary['home']['name']} {fs['HOME']}"
          f"   ({summary['periods_played']} periods, {summary['possessions']} possessions)")
    print(f"WINNER: {summary['winner']}")
    for key in ("away", "home"):
        block = summary[key]
        print("\n" + "-" * 78)
        print(f"{block['name']}  ({block['score']} pts)")
        print(f"{'PLAYER':<20}{'MIN':>5}{'PTS':>5}{'FG':>8}{'3P':>8}{'FT':>8}"
              f"{'REB':>5}{'AST':>4}{'STL':>4}{'BLK':>4}{'TO':>4}")
        for p in block["players"]:
            if p["min"] <= 0:
                continue
            print(f"{p['name']:<20}{p['min']:>5.0f}{p['pts']:>5}"
                  f"{(str(p['fgm'])+'/'+str(p['fga'])):>8}"
                  f"{(str(p['tpm'])+'/'+str(p['tpa'])):>8}"
                  f"{(str(p['ftm'])+'/'+str(p['fta'])):>8}"
                  f"{p['reb']:>5}{p['ast']:>4}{p['stl']:>4}{p['blk']:>4}{p['tov']:>4}")
        t = block["totals"]
        print(f"{'TEAM TOTALS':<20}{'':>5}{t['pts']:>5}"
              f"{(str(t['fgm'])+'/'+str(t['fga'])):>8}"
              f"{(str(t['tpm'])+'/'+str(t['tpa'])):>8}"
              f"{(str(t['ftm'])+'/'+str(t['fta'])):>8}"
              f"{t['reb']:>5}{t['ast']:>4}{t['stl']:>4}{t['blk']:>4}{t['tov']:>4}")


if __name__ == "__main__":
    from rosters_2005 import SPURS_2005, PISTONS_2005

    print("=" * 78)
    print("  2005 NBA FINALS REMATCH  --  Deterministic Simulation Engine")
    print("=" * 78)

    home = build_team("San Antonio Spurs", "HOME", SPURS_2005)
    away = build_team("Detroit Pistons", "AWAY", PISTONS_2005)

    engine = NBAUnifiedEngine(home=home, away=away, seed=2005, difficulty=0.7)

    print("\n--- KEY PLAYS ---")
    summary = engine.simulate_game(verbose=True)
    _print_box(summary)
