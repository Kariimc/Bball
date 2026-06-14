# ==============================================================================
#                               PRESENTATION.PY
# ==============================================================================
# The non-gameplay show: crowd, mascot, cheerleaders, broadcast camera, benches,
# and coaches. Everything here is PRESENTATION ONLY -- it reacts to game events
# and produces serializable telemetry + animation intents for a renderer
# (Godot, etc.) to attach 3D models/animations to. It never affects play.
#
# Actors are intentionally lightweight (position + animation hooks) so we don't
# instantiate hundreds of full athlete proxies for the crowd.
# ==============================================================================

from __future__ import annotations
from enum import Enum, auto
from typing import Any, Dict, List, Optional, Tuple

from nba_comprehensive_game_engine import Vector3
from game_events import EventBus, GameEvent


# ------------------------------------------------------------------ enums


class CrowdState(Enum):
    SITTING_DEJECTED = auto()
    SITTING_IDLE = auto()
    STANDING_EXCITED = auto()
    STANDING_SUPER_EXCITED = auto()


class CameraMode(Enum):
    TRACK_BALL = auto()
    TRACK_CELEBRATION = auto()
    RECORD_CROWD = auto()


class BenchState(Enum):
    SITTING_IDLE = auto()
    WATCHING_INTENSELY = auto()
    BENCH_MOB_CELEBRATION = auto()
    TIMEOUT_HUDDLE = auto()


class CoachState(Enum):
    SIDELINE_PACING = auto()
    CALL_PLAY = auto()
    ARGUING_CALL = auto()
    TIMEOUT_HUDDLE = auto()
    EJECTED = auto()


# ------------------------------------------------------------------ actor


class Actor:
    """A lightweight, model-agnostic presentation entity. A renderer reads
    `position` + `current_anim` and plays the matching clip on its 3D model."""

    def __init__(self, actor_id: str, position: Optional[Vector3] = None):
        self.id = actor_id
        self.position = position or Vector3()
        self.current_anim = "Idle"
        self.active_animations: List[str] = []

    def play_loop(self, name: str, speed_mult: float = 1.0) -> None:
        self.current_anim = name
        self.active_animations = [f"{name}@loop@{speed_mult:.2f}"]

    def trigger_one_shot_action(self, name: str, weight: float = 1.0) -> None:
        self.active_animations.append(f"{name}#{weight:.2f}")

    def to_dict(self) -> Dict[str, Any]:
        return {"id": self.id, "pos": [self.position.x, self.position.y, self.position.z],
                "anim": self.current_anim}


# ------------------------------------------------------------------ crowd


class DynamicCrowdSystem:
    """Stadium emotion driven by score margin + scoring runs. Modeled as seating
    sections (not thousands of objects) each carrying a state/animation; tracks a
    0-1 home momentum the rest of the show can read."""

    _ANIM = {
        CrowdState.SITTING_IDLE: "Idling_Seated",
        CrowdState.SITTING_DEJECTED: "Dejected_Head_In_Hands",
        CrowdState.STANDING_EXCITED: "Cheering_Standing",
        CrowdState.STANDING_SUPER_EXCITED: "Mosh_Pit_Celebration",
    }

    def __init__(self, sections: int = 12):
        self.state = CrowdState.SITTING_IDLE
        self.home_momentum = 0.5
        self.sections: List[Actor] = [Actor(f"crowd_{i}") for i in range(sections)]
        self._apply()

    def synchronize_state(self, home_score: int, away_score: int, current_run: int) -> None:
        diff = home_score - away_score
        old = self.state
        if current_run >= 8:
            self.state = CrowdState.STANDING_SUPER_EXCITED
        elif diff >= 10:
            self.state = CrowdState.STANDING_EXCITED
        elif diff <= -10:
            self.state = CrowdState.SITTING_DEJECTED
        else:
            self.state = CrowdState.SITTING_IDLE

        # Momentum eases toward a target set by margin + run.
        target = 0.5 + max(-0.45, min(0.45, (diff * 0.02) + (current_run * 0.03)))
        self.home_momentum += (target - self.home_momentum) * 0.5
        if self.state != old:
            self._apply()

    def _apply(self) -> None:
        anim = self._ANIM[self.state]
        for i, section in enumerate(self.sections):
            section.play_loop(anim, speed_mult=max(0.8, 1.3 - i * 0.04))

    def react_cannon(self) -> None:
        for s in self.sections:
            s.trigger_one_shot_action("Scramble_For_TShirt")

    def to_telemetry(self) -> Dict[str, Any]:
        return {"state": self.state.name, "home_momentum": round(self.home_momentum, 3),
                "sections": [s.to_dict() for s in self.sections]}


# ------------------------------------------------------------------ entertainment


class EntertainmentManager:
    """Mascot + cheerleaders: idle ambiance, score reactions, halftime show, and
    a t-shirt cannon that returns a launch vector for the renderer to animate."""

    def __init__(self, crowd: DynamicCrowdSystem, cheerleaders: int = 6):
        self.crowd = crowd
        self.mascot = Actor("mascot", Vector3(25.0, 0.0, 47.0))
        self.cheerleaders = [Actor(f"cheer_{i}", Vector3(2.0 + i * 2.0, 0.0, 47.0)) for i in range(cheerleaders)]
        self.halftime_active = False

    def on_score(self, by_home: bool) -> None:
        self.mascot.trigger_one_shot_action("Mascot_Hype")
        for c in self.cheerleaders:
            c.trigger_one_shot_action("Pompom_Cheer")

    def trigger_halftime_show(self) -> None:
        self.halftime_active = True
        self.mascot.play_loop("Mascot_Halftime_Routine")
        for c in self.cheerleaders:
            c.play_loop("Synchronized_Halftime_Dance")

    def end_halftime(self) -> None:
        self.halftime_active = False
        self.mascot.play_loop("Idle")
        for c in self.cheerleaders:
            c.play_loop("Idle")

    def fire_mascot_tshirt_cannon(self, target_seat: Vector3) -> Tuple[Vector3, Vector3]:
        origin = self.mascot.position + Vector3(0.0, 1.4, 0.0)
        arc = (target_seat - origin)
        arc.y += 3.5
        velocity = arc.normalized() * 18.2
        self.crowd.react_cannon()
        self.mascot.trigger_one_shot_action("Fire_Cannon")
        return origin, velocity

    def to_telemetry(self) -> Dict[str, Any]:
        return {"halftime": self.halftime_active, "mascot": self.mascot.to_dict(),
                "cheerleaders": [c.to_dict() for c in self.cheerleaders]}


# ------------------------------------------------------------------ camera


class CameraDirector:
    """Broadcast camera: tracks the ball, cuts to celebrations on big plays, and
    pans the crowd on stoppages. Holds a cut for a few seconds, then resumes."""

    def __init__(self):
        self.mode = CameraMode.TRACK_BALL
        self.target = Vector3(25.0, 0.0, 47.0)
        self._hold = 0.0

    def follow_ball(self, ball_pos: Vector3) -> None:
        if self._hold <= 0.0:
            self.mode = CameraMode.TRACK_BALL
            self.target = ball_pos

    def cut_to_celebration(self, at: Vector3, hold: float = 3.0) -> None:
        self.mode = CameraMode.TRACK_CELEBRATION
        self.target = at
        self._hold = hold

    def cut_to_crowd(self, hold: float = 4.0) -> None:
        self.mode = CameraMode.RECORD_CROWD
        self._hold = hold

    def update(self, dt: float) -> None:
        if self._hold > 0.0:
            self._hold = max(0.0, self._hold - dt)

    def to_telemetry(self) -> Dict[str, Any]:
        return {"mode": self.mode.name, "target": [self.target.x, self.target.y, self.target.z],
                "hold_remaining": round(self._hold, 2)}


# ------------------------------------------------------------------ bench / coach


class SidelineUnit:
    """A team's bench mob + coach, each a stateful animated actor."""

    def __init__(self, side: str):
        self.side = side
        self.bench = Actor(f"{side.lower()}_bench")
        self.coach = Actor(f"{side.lower()}_coach")
        self.bench_state = BenchState.WATCHING_INTENSELY
        self.coach_state = CoachState.SIDELINE_PACING
        self._refresh()

    def set_bench(self, state: BenchState) -> None:
        self.bench_state = state
        self._refresh()

    def set_coach(self, state: CoachState) -> None:
        self.coach_state = state
        self._refresh()

    def _refresh(self) -> None:
        self.bench.play_loop(self.bench_state.name.title())
        self.coach.play_loop(self.coach_state.name.title())

    def to_telemetry(self) -> Dict[str, Any]:
        return {"side": self.side, "bench_state": self.bench_state.name,
                "coach_state": self.coach_state.name,
                "bench": self.bench.to_dict(), "coach": self.coach.to_dict()}


# ------------------------------------------------------------------ director


class PresentationDirector:
    """Wires crowd/entertainment/camera/benches/coaches to the event bus and
    exposes one combined telemetry frame for a renderer."""

    def __init__(self, bus: EventBus):
        self.bus = bus
        self.crowd = DynamicCrowdSystem()
        self.entertainment = EntertainmentManager(self.crowd)
        self.camera = CameraDirector()
        self.home_sideline = SidelineUnit("HOME")
        self.away_sideline = SidelineUnit("AWAY")
        self._run = 0           # current scoring run (signed: + = home)
        self._home = 0
        self._away = 0

        bus.subscribe(GameEvent.SCORE, self._on_score)
        bus.subscribe(GameEvent.DUNK, self._on_dunk)
        bus.subscribe(GameEvent.FOUL, self._on_foul)
        bus.subscribe(GameEvent.VIOLATION, self._on_stoppage)
        bus.subscribe(GameEvent.PERIOD_END, self._on_period_end)
        bus.subscribe(GameEvent.TIMEOUT, self._on_timeout)

    def _sideline(self, side: str) -> SidelineUnit:
        return self.home_sideline if side == "HOME" else self.away_sideline

    def _on_score(self, p: Dict[str, Any]) -> None:
        team = p.get("team", "HOME")
        points = p.get("points", 2)
        if team == "HOME":
            self._home += points
            self._run = self._run + points if self._run >= 0 else points
            self.home_sideline.set_bench(BenchState.BENCH_MOB_CELEBRATION if points >= 3 else BenchState.WATCHING_INTENSELY)
        else:
            self._away += points
            self._run = self._run - points if self._run <= 0 else -points
            self.away_sideline.set_bench(BenchState.BENCH_MOB_CELEBRATION if points >= 3 else BenchState.WATCHING_INTENSELY)
        self.crowd.synchronize_state(self._home, self._away, abs(self._run))
        self.entertainment.on_score(by_home=(team == "HOME"))
        at = Vector3(*p["at"]) if "at" in p else self.camera.target
        self.camera.cut_to_celebration(at)

    def _on_dunk(self, p: Dict[str, Any]) -> None:
        at = Vector3(*p["at"]) if "at" in p else self.camera.target
        self.camera.cut_to_celebration(at, hold=3.5)

    def _on_foul(self, p: Dict[str, Any]) -> None:
        fouling = p.get("team")
        if fouling:
            self._sideline(fouling).set_coach(CoachState.ARGUING_CALL)
        if p.get("technical"):
            self._sideline(fouling).set_coach(CoachState.EJECTED)

    def _on_stoppage(self, p: Dict[str, Any]) -> None:
        self.camera.cut_to_crowd(hold=2.0)

    def _on_period_end(self, p: Dict[str, Any]) -> None:
        self.camera.cut_to_crowd(hold=4.0)
        if p.get("quarter") == 2:
            self.entertainment.trigger_halftime_show()

    def _on_timeout(self, p: Dict[str, Any]) -> None:
        for s in (self.home_sideline, self.away_sideline):
            s.set_bench(BenchState.TIMEOUT_HUDDLE)
            s.set_coach(CoachState.TIMEOUT_HUDDLE)
        self.camera.cut_to_crowd(hold=3.0)

    def update(self, dt: float, ball_pos: Optional[Vector3] = None) -> None:
        self.camera.update(dt)
        if ball_pos is not None:
            self.camera.follow_ball(ball_pos)

    def to_telemetry(self) -> Dict[str, Any]:
        return {
            "crowd": self.crowd.to_telemetry(),
            "entertainment": self.entertainment.to_telemetry(),
            "camera": self.camera.to_telemetry(),
            "sidelines": [self.home_sideline.to_telemetry(), self.away_sideline.to_telemetry()],
        }

    def snapshot(self) -> Dict[str, Any]:
        """Compact broadcast frame for per-possession streaming (no actor arrays)."""
        return {
            "crowd_state": self.crowd.state.name,
            "home_momentum": round(self.crowd.home_momentum, 3),
            "camera": self.camera.mode.name,
            "home_coach": self.home_sideline.coach_state.name,
            "away_coach": self.away_sideline.coach_state.name,
            "mascot": self.entertainment.mascot.current_anim,
        }
