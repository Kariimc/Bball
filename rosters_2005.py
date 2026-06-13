# ==============================================================================
#                              ROSTERS_2005.PY
# ==============================================================================
# Authentic 2004-05 NBA Finals lens: San Antonio Spurs vs. Detroit Pistons.
#
# DATA NOTE: Ratings are archetype-derived approximations (0-100 scale, except
# height_inches and vertical_leap which are physical inches). They are tuned to
# reproduce each player's *relative* real-world strengths from that season
# (e.g. Ben Wallace = elite defense/rebounding, poor shooting; Bruce Bowen =
# lockdown perimeter D, corner-three only). They are NOT official 2K-style
# numbers. Pure data, no logic — consumed by the engine's entity builder.
# ==============================================================================

from __future__ import annotations
from typing import Dict, List, TypedDict


class PlayerData(TypedDict):
    name: str
    role: str          # PG, SG, SF, PF, C
    height_inches: float
    stats: Dict[str, int]


# Canonical stat keys the engine understands. Any omitted key falls back to the
# engine's DEFAULT_STATS so partial roster data still loads cleanly.
#   shooting:   free_throw, shot_close, mid_range, three_point
#   athletic:   dunk_rating, vertical_leap (inches), speed, stamina
#   skill:      passing_accuracy, ball_handle, physical_strength, hustle
#   defense:    perimeter_defense, interior_defense, defensive_awareness,
#               rebounding, steal, block


SPURS_2005: List[PlayerData] = [
    {
        "name": "Tony Parker", "role": "PG", "height_inches": 74.0,
        "stats": {
            "free_throw": 70, "shot_close": 84, "mid_range": 76, "three_point": 55,
            "dunk_rating": 58, "vertical_leap": 33, "speed": 94, "stamina": 88,
            "passing_accuracy": 82, "ball_handle": 87, "physical_strength": 62, "hustle": 80,
            "perimeter_defense": 68, "interior_defense": 45, "defensive_awareness": 70,
            "rebounding": 35, "steal": 70, "block": 25,
        },
    },
    {
        "name": "Manu Ginobili", "role": "SG", "height_inches": 78.0,
        "stats": {
            "free_throw": 80, "shot_close": 83, "mid_range": 80, "three_point": 80,
            "dunk_rating": 70, "vertical_leap": 35, "speed": 86, "stamina": 84,
            "passing_accuracy": 84, "ball_handle": 85, "physical_strength": 68, "hustle": 92,
            "perimeter_defense": 80, "interior_defense": 52, "defensive_awareness": 82,
            "rebounding": 48, "steal": 86, "block": 35,
        },
    },
    {
        "name": "Bruce Bowen", "role": "SF", "height_inches": 79.0,
        "stats": {
            "free_throw": 60, "shot_close": 55, "mid_range": 50, "three_point": 74,
            "dunk_rating": 45, "vertical_leap": 28, "speed": 74, "stamina": 90,
            "passing_accuracy": 60, "ball_handle": 58, "physical_strength": 72, "hustle": 88,
            "perimeter_defense": 96, "interior_defense": 60, "defensive_awareness": 92,
            "rebounding": 45, "steal": 72, "block": 40,
        },
    },
    {
        "name": "Tim Duncan", "role": "PF", "height_inches": 83.0,
        "stats": {
            "free_throw": 67, "shot_close": 88, "mid_range": 84, "three_point": 20,
            "dunk_rating": 84, "vertical_leap": 31, "speed": 66, "stamina": 86,
            "passing_accuracy": 78, "ball_handle": 60, "physical_strength": 88, "hustle": 85,
            "perimeter_defense": 62, "interior_defense": 95, "defensive_awareness": 94,
            "rebounding": 92, "steal": 50, "block": 88,
        },
    },
    {
        "name": "Rasho Nesterovic", "role": "C", "height_inches": 84.0,
        "stats": {
            "free_throw": 62, "shot_close": 72, "mid_range": 58, "three_point": 15,
            "dunk_rating": 72, "vertical_leap": 27, "speed": 58, "stamina": 80,
            "passing_accuracy": 58, "ball_handle": 40, "physical_strength": 82, "hustle": 72,
            "perimeter_defense": 48, "interior_defense": 80, "defensive_awareness": 74,
            "rebounding": 80, "steal": 38, "block": 76,
        },
    },
    {
        "name": "Brent Barry", "role": "SG", "height_inches": 79.0,
        "stats": {
            "free_throw": 84, "shot_close": 70, "mid_range": 74, "three_point": 82,
            "dunk_rating": 60, "vertical_leap": 30, "speed": 76, "stamina": 78,
            "passing_accuracy": 78, "ball_handle": 76, "physical_strength": 66, "hustle": 70,
            "perimeter_defense": 66, "interior_defense": 48, "defensive_awareness": 70,
            "rebounding": 44, "steal": 64, "block": 30,
        },
    },
    {
        "name": "Robert Horry", "role": "PF", "height_inches": 82.0,
        "stats": {
            "free_throw": 74, "shot_close": 68, "mid_range": 70, "three_point": 78,
            "dunk_rating": 62, "vertical_leap": 28, "speed": 64, "stamina": 76,
            "passing_accuracy": 70, "ball_handle": 62, "physical_strength": 78, "hustle": 80,
            "perimeter_defense": 72, "interior_defense": 76, "defensive_awareness": 88,
            "rebounding": 74, "steal": 58, "block": 64,
        },
    },
    {
        "name": "Nazr Mohammed", "role": "C", "height_inches": 82.0,
        "stats": {
            "free_throw": 62, "shot_close": 74, "mid_range": 48, "three_point": 10,
            "dunk_rating": 76, "vertical_leap": 30, "speed": 60, "stamina": 78,
            "passing_accuracy": 50, "ball_handle": 42, "physical_strength": 84, "hustle": 78,
            "perimeter_defense": 46, "interior_defense": 76, "defensive_awareness": 68,
            "rebounding": 84, "steal": 40, "block": 70,
        },
    },
]


PISTONS_2005: List[PlayerData] = [
    {
        "name": "Chauncey Billups", "role": "PG", "height_inches": 75.0,
        "stats": {
            "free_throw": 90, "shot_close": 78, "mid_range": 80, "three_point": 80,
            "dunk_rating": 58, "vertical_leap": 29, "speed": 80, "stamina": 86,
            "passing_accuracy": 84, "ball_handle": 85, "physical_strength": 80, "hustle": 82,
            "perimeter_defense": 78, "interior_defense": 50, "defensive_awareness": 82,
            "rebounding": 48, "steal": 74, "block": 28,
        },
    },
    {
        "name": "Richard Hamilton", "role": "SG", "height_inches": 79.0,
        "stats": {
            "free_throw": 86, "shot_close": 80, "mid_range": 90, "three_point": 70,
            "dunk_rating": 60, "vertical_leap": 32, "speed": 88, "stamina": 96,
            "passing_accuracy": 78, "ball_handle": 80, "physical_strength": 64, "hustle": 90,
            "perimeter_defense": 74, "interior_defense": 46, "defensive_awareness": 76,
            "rebounding": 42, "steal": 70, "block": 30,
        },
    },
    {
        "name": "Tayshaun Prince", "role": "SF", "height_inches": 81.0,
        "stats": {
            "free_throw": 76, "shot_close": 74, "mid_range": 76, "three_point": 72,
            "dunk_rating": 70, "vertical_leap": 33, "speed": 80, "stamina": 88,
            "passing_accuracy": 76, "ball_handle": 72, "physical_strength": 66, "hustle": 84,
            "perimeter_defense": 88, "interior_defense": 64, "defensive_awareness": 86,
            "rebounding": 58, "steal": 72, "block": 60,
        },
    },
    {
        "name": "Rasheed Wallace", "role": "PF", "height_inches": 83.0,
        "stats": {
            "free_throw": 72, "shot_close": 80, "mid_range": 82, "three_point": 74,
            "dunk_rating": 76, "vertical_leap": 30, "speed": 68, "stamina": 82,
            "passing_accuracy": 74, "ball_handle": 64, "physical_strength": 84, "hustle": 76,
            "perimeter_defense": 74, "interior_defense": 88, "defensive_awareness": 88,
            "rebounding": 80, "steal": 58, "block": 84,
        },
    },
    {
        "name": "Ben Wallace", "role": "C", "height_inches": 81.0,
        "stats": {
            "free_throw": 42, "shot_close": 64, "mid_range": 38, "three_point": 5,
            "dunk_rating": 78, "vertical_leap": 36, "speed": 72, "stamina": 92,
            "passing_accuracy": 52, "ball_handle": 44, "physical_strength": 96, "hustle": 96,
            "perimeter_defense": 70, "interior_defense": 97, "defensive_awareness": 96,
            "rebounding": 97, "steal": 72, "block": 92,
        },
    },
    {
        "name": "Lindsey Hunter", "role": "PG", "height_inches": 74.0,
        "stats": {
            "free_throw": 74, "shot_close": 62, "mid_range": 64, "three_point": 66,
            "dunk_rating": 50, "vertical_leap": 29, "speed": 82, "stamina": 84,
            "passing_accuracy": 68, "ball_handle": 74, "physical_strength": 64, "hustle": 88,
            "perimeter_defense": 86, "interior_defense": 46, "defensive_awareness": 82,
            "rebounding": 40, "steal": 84, "block": 26,
        },
    },
    {
        "name": "Antonio McDyess", "role": "PF", "height_inches": 81.0,
        "stats": {
            "free_throw": 78, "shot_close": 78, "mid_range": 78, "three_point": 30,
            "dunk_rating": 76, "vertical_leap": 32, "speed": 70, "stamina": 80,
            "passing_accuracy": 66, "ball_handle": 56, "physical_strength": 82, "hustle": 82,
            "perimeter_defense": 64, "interior_defense": 80, "defensive_awareness": 78,
            "rebounding": 82, "steal": 50, "block": 70,
        },
    },
    {
        "name": "Carlos Arroyo", "role": "PG", "height_inches": 73.0,
        "stats": {
            "free_throw": 80, "shot_close": 72, "mid_range": 70, "three_point": 62,
            "dunk_rating": 48, "vertical_leap": 27, "speed": 80, "stamina": 78,
            "passing_accuracy": 80, "ball_handle": 80, "physical_strength": 60, "hustle": 74,
            "perimeter_defense": 60, "interior_defense": 42, "defensive_awareness": 66,
            "rebounding": 38, "steal": 62, "block": 22,
        },
    },
]


# Convenience registry so callers can look teams up by short key.
ROSTERS_2005: Dict[str, List[PlayerData]] = {
    "SAS": SPURS_2005,
    "DET": PISTONS_2005,
}

TEAM_NAMES_2005: Dict[str, str] = {
    "SAS": "San Antonio Spurs",
    "DET": "Detroit Pistons",
}
