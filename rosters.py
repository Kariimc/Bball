# ==============================================================================
#                                 ROSTERS.PY
# ==============================================================================
# Aggregator: one lookup surface over every available team across all eras.
# Keys are unique across modules (2005: SAS, DET | modern: BOS, DEN, OKC, ...).
# ==============================================================================

from __future__ import annotations
from typing import Dict, List

from rosters_2005 import ROSTERS_2005, TEAM_NAMES_2005
from rosters_modern import ROSTERS_MODERN, TEAM_NAMES_MODERN

ALL_ROSTERS: Dict[str, List[dict]] = {**ROSTERS_2005, **ROSTERS_MODERN}
ALL_TEAM_NAMES: Dict[str, str] = {**TEAM_NAMES_2005, **TEAM_NAMES_MODERN}

ERAS: Dict[str, List[str]] = {
    "2005": list(ROSTERS_2005.keys()),
    "modern": list(ROSTERS_MODERN.keys()),
}


def get_roster(key: str) -> List[dict]:
    key = key.upper()
    if key not in ALL_ROSTERS:
        raise KeyError(f"Unknown team '{key}'. Available: {sorted(ALL_ROSTERS)}")
    return ALL_ROSTERS[key]


def team_name(key: str) -> str:
    return ALL_TEAM_NAMES[key.upper()]


def all_teams() -> Dict[str, str]:
    return dict(ALL_TEAM_NAMES)
