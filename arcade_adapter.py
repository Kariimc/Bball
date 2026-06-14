# ==============================================================================
#                              ARCADE_ADAPTER.PY
# ==============================================================================
# Bridges the single source of truth (the 18-stat engine rosters: 2005, modern,
# and URL-imported teams) into the 4-stat model the real-time arcade game uses.
# Deliberately pygame-free so it stays unit-testable and the data layer is never
# duplicated -- the arcade game and the sim engine read the SAME rosters.
# ==============================================================================

from __future__ import annotations
from typing import Any, Dict, List, Tuple

from rosters import get_roster, team_name

# Arcade stat keys consumed by voxel_hoops.Player.
ARCADE_KEYS = ("speed", "vertical", "steal", "shooting")


def _clamp10(value: float) -> int:
    return int(max(1, min(10, round(value))))


def to_arcade_stats(ratings: Dict[str, Any]) -> Dict[str, int]:
    """Map a player's engine ratings (0-100, vertical_leap in inches) to the
    arcade game's 1-10 scale."""
    speed = _clamp10(ratings.get("speed", 75) / 10.0)
    # vertical_leap is inches (~20-44); compress to 1-10.
    vertical = _clamp10((ratings.get("vertical_leap", 30) - 14) / 3.0)
    steal = _clamp10(ratings.get("steal", 60) / 10.0)
    shooting = _clamp10(
        (ratings.get("three_point", 70) + ratings.get("mid_range", 72)
         + ratings.get("shot_close", 75)) / 30.0)
    return {"speed": speed, "vertical": vertical, "steal": steal, "shooting": shooting}


def _arcade_overall(arcade: Dict[str, int]) -> int:
    return sum(arcade[k] for k in ARCADE_KEYS)


def build_arcade_team(key: str) -> List[Dict[str, Any]]:
    """Return a team's players as arcade dicts, sorted strongest first."""
    players: List[Dict[str, Any]] = []
    for p in get_roster(key):
        arcade = to_arcade_stats(p.get("stats", {}))
        players.append({
            "name": p["name"],
            "role": p.get("role", "SF"),
            "height_inches": float(p.get("height_inches", 78.0)),
            "stats": arcade,
            "overall": _arcade_overall(arcade),
        })
    players.sort(key=lambda d: d["overall"], reverse=True)
    return players


def get_arcade_lineup(key: str, size: int = 5) -> List[Dict[str, Any]]:
    """A starting lineup that covers PG/SG/SF/PF/C, then fills by overall."""
    players = build_arcade_team(key)
    chosen: List[Dict[str, Any]] = []
    for role in ("PG", "SG", "SF", "PF", "C"):
        for p in players:
            if p["role"] == role and p not in chosen:
                chosen.append(p)
                break
    for p in players:
        if len(chosen) >= size:
            break
        if p not in chosen:
            chosen.append(p)
    return chosen[:size]


def get_arcade_matchup(home_key: str, away_key: str, size: int = 5
                       ) -> Tuple[str, List[Dict[str, Any]], str, List[Dict[str, Any]]]:
    """Resolve two team keys into (home_name, home_five, away_name, away_five)."""
    return (team_name(home_key), get_arcade_lineup(home_key, size),
            team_name(away_key), get_arcade_lineup(away_key, size))
