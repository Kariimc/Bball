# ==============================================================================
#                                COURT_RULES.PY
# ==============================================================================
# Pure, pygame-free court geometry + scoring rules. Lives apart from the render
# loop so the game's rules are the single source of truth AND unit-testable
# without a display. voxel_hoops.py imports everything here -- no duplication.
#
# Coordinate space (feet): x in [0, COURT_LENGTH] along the floor, y in
# [0, COURT_WIDTH] across it, z is height. Home attacks the right hoop; away
# attacks the left hoop.
# ==============================================================================

from __future__ import annotations
import math
from typing import Tuple

COURT_WIDTH = 50
COURT_LENGTH = 94

RIM_HEIGHT = 15.0
RIM_Z_LOW = 13.5
RIM_Z_HIGH = 16.0
SCORE_RADIUS = 2.6          # how close (x,y) must be to the rim to drop in
THREE_POINT_DIST = 22.0     # >= this from the hoop counts as a three
DUNK_RANGE = 18.0           # close enough to attempt a dunk
SHOOT_RANGE = 30.0          # AI won't heave from beyond this

HOME_HOOP: Tuple[float, float] = (COURT_LENGTH - 4, COURT_WIDTH / 2)   # right
AWAY_HOOP: Tuple[float, float] = (4, COURT_WIDTH / 2)                  # left


def distance(ax: float, ay: float, bx: float, by: float) -> float:
    return math.hypot(ax - bx, ay - by)


def target_hoop(team: str) -> Tuple[float, float]:
    """The hoop a team is attacking."""
    return HOME_HOOP if team == "home" else AWAY_HOOP


def point_value(x: float, y: float, team: str) -> int:
    """2 or 3 points for a shot taken from (x, y) by `team`."""
    hx, hy = target_hoop(team)
    return 3 if distance(x, y, hx, hy) >= THREE_POINT_DIST else 2


def is_scoring_position(ball_x: float, ball_y: float, ball_z: float,
                        hoop: Tuple[float, float]) -> bool:
    """True if a falling ball is in the cylinder above a rim."""
    return RIM_Z_LOW <= ball_z <= RIM_Z_HIGH and distance(ball_x, ball_y, *hoop) < SCORE_RADIUS


def in_bounds(x: float, y: float) -> bool:
    return 0 <= x <= COURT_LENGTH and 0 <= y <= COURT_WIDTH


def format_clock(seconds: float) -> str:
    seconds = max(0, int(seconds))
    return f"{seconds // 60}:{seconds % 60:02d}"
