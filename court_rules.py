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
SHOOT_RANGE = 24.0          # AI won't shoot (and rarely heaves) beyond this

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


# -- 5-on-5 spacing -----------------------------------------------------------
# Half-court offensive spots by role, in court feet. Home attacks the right
# hoop (~x=90); away attacks the left (~x=4). These are mirror images.
OFFENSE_FORMATION = {
    "home": {"PG": (66, 25), "SG": (74, 13), "SF": (74, 37), "PF": (82, 18), "C": (85, 32)},
    "away": {"PG": (28, 25), "SG": (20, 13), "SF": (20, 37), "PF": (12, 18), "C": (9, 32)},
}


def offensive_spot(team: str, role: str) -> Tuple[float, float]:
    """Where a player should space to when their team has the ball."""
    return OFFENSE_FORMATION[team].get(role, (COURT_LENGTH / 2, COURT_WIDTH / 2))


# -- shot model (tunable, deterministic-on-launch) ----------------------------
def make_probability(shooting: int, dist: float, contested: bool = False) -> float:
    """Probability a shot drops, from a 1-10 shooting rating and distance (ft).
    Decoupled from projectile physics so scoring is reliable and tunable."""
    if dist < 8.0:
        base = 0.60          # layup / close range
    elif dist < THREE_POINT_DIST:
        base = 0.44          # mid-range
    else:
        base = 0.36          # three
    base += (shooting - 5) * 0.025
    if contested:
        base -= 0.15
    return max(0.05, min(0.95, base))
