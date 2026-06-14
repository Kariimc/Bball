# ==============================================================================
#                                GAME_EVENTS.PY
# ==============================================================================
# A tiny, dependency-free event bus. Gameplay code EMITS events; the
# presentation layer (crowd, mascots, cheerleaders, camera, bench, coaches) and
# the officiating layer SUBSCRIBE and react. This keeps everything that touches
# play cleanly separated from everything that's reaction/ambiance, and lets a
# renderer (Godot, etc.) attach without the simulation knowing it exists.
# ==============================================================================

from __future__ import annotations
from enum import Enum, auto
from typing import Any, Callable, Dict, List


class GameEvent(Enum):
    TIP_OFF = auto()
    SCORE = auto()                # payload: team, points, scorer, dunk
    MADE_THREE = auto()
    DUNK = auto()
    STEAL = auto()
    BLOCK = auto()
    TURNOVER = auto()
    FOUL = auto()
    VIOLATION = auto()
    FREE_THROW = auto()           # payload: shooter, made
    INJURY = auto()
    PERIOD_END = auto()
    TIMEOUT = auto()
    GAME_OVER = auto()


Handler = Callable[[Dict[str, Any]], None]


class EventBus:
    """Synchronous pub/sub. Handlers run in subscription order; one failing
    handler never blocks the others or the simulation."""

    def __init__(self) -> None:
        self._subs: Dict[GameEvent, List[Handler]] = {}
        self.history: List[Dict[str, Any]] = []

    def subscribe(self, event: GameEvent, handler: Handler) -> None:
        self._subs.setdefault(event, []).append(handler)

    def emit(self, event: GameEvent, **payload: Any) -> Dict[str, Any]:
        record = {"event": event.name, **payload}
        self.history.append(record)
        for handler in self._subs.get(event, ()):
            try:
                handler(payload)
            except Exception as exc:  # presentation must never crash the game
                record.setdefault("handler_errors", []).append(str(exc))
        return record

    def clear_history(self) -> None:
        self.history.clear()
