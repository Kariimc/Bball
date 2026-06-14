# ==============================================================================
#                                OFFICIATING.PY
# ==============================================================================
# The officials + real-time rulebook for the live (rendered) game:
#   * RefereeAI       -- state-driven movement: jump ball, retrieve, hand off.
#   * LiveOfficiatingEngine -- shot clock, backcourt, closely-guarded, 3-second,
#                         traveling, double-dribble, carry, fouls + bonus, techs.
#   * JumpBallSimulation -- gravitational tip kinematics + tip-timing windows.
#   * FreeThrowSystem -- lane placement + sequential free-throw resolution.
#
# Reuses Vector3/EntityProxy from the engine (no duplicate math/identity) and
# emits events through the shared EventBus so the presentation layer reacts.
# Every method returns serializable telemetry for a renderer.
# ==============================================================================

from __future__ import annotations
import math
import random
from enum import Enum, auto
from typing import Any, Dict, List, Optional, Tuple

from nba_comprehensive_game_engine import Vector3, EntityProxy
from game_events import EventBus, GameEvent

REGULATION_QUARTER = 720.0
SHOT_CLOCK = 24.0
BONUS_TEAM_FOULS = 5
BONUS_LAST_TWO_MIN = 2


class RefState(Enum):
    IDLE = auto()
    APPROACH_JUMP_BALL = auto()
    EXECUTE_JUMP_BALL = auto()
    RETRIEVE_BALL = auto()
    HANDOFF_BALL = auto()


# ------------------------------------------------------------------ referee


class RefereeAI:
    """A single official that walks to targets and shuttles the ball. Operates on
    any ball object exposing `.position` (and optional `.freeze`)."""

    def __init__(self, ball: Any, start: Optional[Vector3] = None, speed: float = 4.2):
        self.state = RefState.IDLE
        self.position = start or Vector3(-5.0, 0.0, 25.0)
        self.ball = ball
        self.target_player: Optional[EntityProxy] = None
        self.is_holding_ball = False
        self.speed = speed

    def update(self, dt: float) -> None:
        if self.state == RefState.APPROACH_JUMP_BALL:
            center = Vector3(25.0, 0.0, 47.0)
            self._move_towards(center, dt)
            if self.position.distance_to(center) < 0.4:
                self.state = RefState.EXECUTE_JUMP_BALL
        elif self.state == RefState.RETRIEVE_BALL:
            self._move_towards(self.ball.position, dt)
            if self.position.distance_to(self.ball.position) < 0.6:
                self._grab()
        elif self.state == RefState.HANDOFF_BALL and self.target_player:
            self._move_towards(self.target_player.position, dt)
            if self.position.distance_to(self.target_player.position) < 0.8:
                self._handoff()

    def trigger_retrieval(self, hand_to: Optional[EntityProxy] = None) -> None:
        self.target_player = hand_to
        if hasattr(self.ball, "freeze"):
            self.ball.freeze = True
        self.state = RefState.RETRIEVE_BALL

    def _move_towards(self, target: Vector3, dt: float) -> None:
        direction = (target - self.position).normalized()
        self.position = self.position + (direction * (self.speed * dt))

    def _grab(self) -> None:
        self.is_holding_ball = True
        self.state = RefState.HANDOFF_BALL if self.target_player else RefState.IDLE

    def _handoff(self) -> None:
        if not self.target_player:
            return
        self.is_holding_ball = False
        self.ball.position = self.target_player.position + Vector3(0.0, 1.1, 0.0)
        if hasattr(self.ball, "freeze"):
            self.ball.freeze = False
        self.state = RefState.IDLE

    def to_telemetry(self) -> Dict[str, Any]:
        return {"state": self.state.name,
                "pos": [self.position.x, self.position.y, self.position.z],
                "holding_ball": self.is_holding_ball}


# ------------------------------------------------------------------ live rules


class LiveOfficiatingEngine:
    """Real-time rulebook. Pass explicit, model-agnostic state in; get optional
    violation/foul telemetry out. Emits events on the bus when one is provided."""

    def __init__(self, bus: Optional[EventBus] = None):
        self.bus = bus
        self.quarter = 1
        self.game_clock = REGULATION_QUARTER
        self.shot_clock = SHOT_CLOCK
        self.team_fouls: Dict[str, int] = {"HOME": 0, "AWAY": 0}
        self.fouls_last_two_min: Dict[str, int] = {"HOME": 0, "AWAY": 0}
        self.is_dead_ball = True
        self._backcourt = 0.0
        self._closely_guarded = 0.0
        self._paint: Dict[str, float] = {}
        self._steps: Dict[str, int] = {}

    # -- clocks ----------------------------------------------------------------
    def update_clocks(self, dt: float, *, ball_carrier: bool = False,
                      in_frontcourt: bool = True, defense_nearby: bool = False
                      ) -> Optional[Dict[str, Any]]:
        if self.is_dead_ball:
            return None
        self.game_clock = max(0.0, self.game_clock - dt)
        self.shot_clock = max(0.0, self.shot_clock - dt)
        if self.shot_clock <= 0:
            return self.trigger_violation("24-SECOND SHOT CLOCK")
        if self.game_clock <= 0:
            return self.handle_period_expiration()
        if ball_carrier:
            if not in_frontcourt:
                self._backcourt += dt
                if self._backcourt > 8.0:
                    return self.trigger_violation("8-SECOND BACKCOURT")
            else:
                self._backcourt = 0.0
            if defense_nearby:
                self._closely_guarded += dt
                if self._closely_guarded > 5.0:
                    return self.trigger_violation("5-SECOND CLOSELY GUARDED")
            else:
                self._closely_guarded = 0.0
        return None

    # -- spatial / handling ----------------------------------------------------
    def record_paint(self, player_id: str, is_offense: bool, in_paint: bool,
                     guarding: bool, dt: float) -> Optional[Dict[str, Any]]:
        if not in_paint:
            self._paint[player_id] = 0.0
            return None
        self._paint[player_id] = self._paint.get(player_id, 0.0) + dt
        if self._paint[player_id] > 3.0:
            self._paint[player_id] = 0.0
            if is_offense:
                return self.trigger_violation("OFFENSIVE 3-SECONDS")
            if not guarding:
                return self.trigger_technical("DEFENSIVE 3-SECONDS")
        return None

    def record_travel(self, player_id: str, moving: bool, gathered: bool,
                      dribbling: bool) -> Optional[Dict[str, Any]]:
        if moving and gathered and not dribbling:
            self._steps[player_id] = self._steps.get(player_id, 0) + 1
            if self._steps[player_id] > 2:
                self._steps[player_id] = 0
                return self.trigger_violation("TRAVELING")
        else:
            self._steps[player_id] = 0
        return None

    def check_double_dribble(self, has_dribbled: bool, currently_dribbling: bool,
                             starting_new_dribble: bool) -> Optional[Dict[str, Any]]:
        if starting_new_dribble and has_dribbled and not currently_dribbling:
            return self.trigger_violation("DOUBLE DRIBBLE")
        return None

    def check_carry(self, dribbling: bool, hand_under_ball: bool) -> Optional[Dict[str, Any]]:
        if dribbling and hand_under_ball:
            return self.trigger_violation("CARRYING / PALMING")
        return None

    # -- fouls -----------------------------------------------------------------
    def commit_foul(self, fouling_team: str, foul_type: str, on_team: str, *,
                    shooting: bool = False, beyond_arc: bool = False,
                    flagrant: bool = False, charge: bool = False) -> Dict[str, Any]:
        if charge:
            return self.trigger_foul("OFFENSIVE CHARGE", "TURNOVER_NO_TEAM_COUNT", on_team=fouling_team)

        self.team_fouls[fouling_team] += 1
        if self.game_clock <= 120.0:
            self.fouls_last_two_min[fouling_team] += 1

        if flagrant:
            return self.trigger_foul("FLAGRANT 1", "2_SHOTS_AND_POSSESSION", on_team, technical=False)
        if shooting:
            penalty = "3_FREE_THROWS" if beyond_arc else "2_FREE_THROWS"
            return self.trigger_foul(foul_type, penalty, on_team)

        in_bonus = (self.team_fouls[fouling_team] >= BONUS_TEAM_FOULS
                    or self.fouls_last_two_min[fouling_team] >= BONUS_LAST_TWO_MIN)
        penalty = "PENALTY_BONUS_2_SHOTS" if in_bonus else "SIDE_INBOUND"
        return self.trigger_foul(foul_type, penalty, on_team)

    # -- triggers --------------------------------------------------------------
    def trigger_violation(self, name: str) -> Dict[str, Any]:
        self.is_dead_ball = True
        rec = {"event": "VIOLATION", "type": name, "turnover": True}
        if self.bus:
            self.bus.emit(GameEvent.VIOLATION, **rec)
        return rec

    def trigger_foul(self, name: str, penalty: str, on_team: str,
                     technical: bool = False) -> Dict[str, Any]:
        self.is_dead_ball = True
        rec = {"event": "FOUL", "type": name, "penalty": penalty,
               "recipient_team": on_team, "technical": technical}
        if self.bus:
            self.bus.emit(GameEvent.FOUL, type=name, penalty=penalty,
                          team=("AWAY" if on_team == "HOME" else "HOME"), technical=technical)
        return rec

    def trigger_technical(self, name: str) -> Dict[str, Any]:
        rec = {"event": "TECHNICAL", "type": name, "penalty": "1_FREE_THROW_RESUME_POI"}
        if self.bus:
            self.bus.emit(GameEvent.FOUL, type=name, technical=True, team="HOME")
        return rec

    def handle_period_expiration(self) -> Dict[str, Any]:
        self.is_dead_ball = True
        self.quarter += 1
        self.game_clock = REGULATION_QUARTER
        self.team_fouls = {"HOME": 0, "AWAY": 0}
        self.fouls_last_two_min = {"HOME": 0, "AWAY": 0}
        rec = {"event": "QUARTER_EXPIRED", "next_quarter": self.quarter}
        if self.bus:
            self.bus.emit(GameEvent.PERIOD_END, quarter=self.quarter - 1)
        return rec

    def reset_shot_clock(self, seconds: float = SHOT_CLOCK) -> None:
        self.shot_clock = seconds

    def to_telemetry(self) -> Dict[str, Any]:
        return {"quarter": self.quarter, "game_clock": round(self.game_clock, 1),
                "shot_clock": round(self.shot_clock, 1),
                "team_fouls": dict(self.team_fouls), "dead_ball": self.is_dead_ball}


# ------------------------------------------------------------------ jump ball


class JumpBallSimulation:
    """Gravitational tip kinematics + AI/human tip-timing windows."""

    def __init__(self, ball: Any, gravity: float = 9.80665):
        self.ball = ball
        self.gravity = gravity
        self.v0 = 7.5
        self.elapsed = 0.0
        self.apex_time = 0.0
        self.active = False

    def initiate_toss(self, initial_velocity: float = 7.5) -> float:
        self.v0 = initial_velocity
        self.elapsed = 0.0
        self.active = True
        if hasattr(self.ball, "freeze"):
            self.ball.freeze = True
        self.apex_time = self.v0 / self.gravity
        return self.apex_time

    def update(self, dt: float) -> None:
        if not self.active:
            return
        self.elapsed += dt
        y = 2.0 + (self.v0 * self.elapsed) - (0.5 * self.gravity * self.elapsed ** 2)
        self.ball.position = Vector3(25.0, max(0.0, y), 47.0)
        if self.elapsed > self.apex_time * 2.0:
            self.active = False

    def evaluate_ai_jump_decision(self, current_time: float, jump_window: float,
                                  mistake_freq: float) -> bool:
        if random.random() < mistake_freq:
            return current_time > (self.apex_time + 0.3)
        return abs(current_time - self.apex_time) <= jump_window

    def process_tip_input(self, input_time: float, window: float = 0.18) -> float:
        delta = abs(input_time - self.apex_time)
        if delta > window:
            return 0.0
        return 1.0 - (delta / window)


# ------------------------------------------------------------------ free throws


class FreeThrowSystem:
    """Lane placement + sequential free-throw resolution. Returns per-shot
    telemetry; the caller applies points (keeps this decoupled from scoring)."""

    def __init__(self, bus: Optional[EventBus] = None, timing_window: float = 0.12):
        self.bus = bus
        self.timing_window = timing_window
        self.shooter: Optional[EntityProxy] = None
        self.total = 0
        self.index = 0
        self.active = False

    def initiate(self, shooter: EntityProxy, shot_count: int) -> Dict[str, Any]:
        self.shooter = shooter
        self.total = shot_count
        self.index = 1
        self.active = True
        # Warp the shooter to the line (renderer reads this for placement).
        shooter.position = Vector3(25.0, 0.0, 19.0)
        shooter.play_anim("FreeThrow_Setup")
        return {"event": "FREE_THROWS", "shooter": shooter.name, "count": shot_count}

    def process_shot(self, release_timing_delta: float = 0.0) -> Dict[str, Any]:
        if not self.active or not self.shooter:
            return {"event": "NO_ACTION"}
        ft = self.shooter.rating("free_throw") / 100.0
        eff = math.exp(-((release_timing_delta / (self.timing_window * 1.2)) ** 2))
        made = random.random() < (ft * eff)
        self.shooter.play_anim("FreeThrow_Make" if made else "FreeThrow_Miss")
        rec = {"event": "FREE_THROW", "shooter": self.shooter.name,
               "shot": self.index, "of": self.total, "made": made}
        if self.bus:
            self.bus.emit(GameEvent.FREE_THROW, shooter=self.shooter.name, made=made)
        if self.index < self.total:
            self.index += 1
        else:
            self.active = False
            rec["sequence_complete"] = True
        return rec

    def to_telemetry(self) -> Dict[str, Any]:
        return {"active": self.active, "shooter": self.shooter.name if self.shooter else None,
                "shot": self.index, "total": self.total}
