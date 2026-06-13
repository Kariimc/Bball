# ==============================================================================
#                              ROSTERS_MODERN.PY
# ==============================================================================
# Fictional teams modeled on the *play-style archetypes* of current NBA
# contenders. All team and player names are invented; the ratings echo each
# real franchise's identity (e.g. a playmaking-hub center, an athletic
# downhill forward, a switch-everything wing-shooting team) without using any
# real player's name or likeness.
#
# Same data contract as rosters_2005.py: plain dicts consumed by the engine's
# build_team(). Ratings 0-100 (height_inches / vertical_leap are inches).
# Stat keys: free_throw, shot_close, mid_range, three_point, dunk_rating,
# vertical_leap, speed, stamina, passing_accuracy, ball_handle,
# physical_strength, hustle, perimeter_defense, interior_defense,
# defensive_awareness, rebounding, steal, block.
# ==============================================================================

from __future__ import annotations
from typing import Dict, List

PlayerData = Dict[str, object]


# -- BOS: switch-everything, elite wing three-point shooting (Celtics-style) ---
BOSTON_SHAMROCKS: List[PlayerData] = [
    {"name": "Marcus Vane", "role": "PG", "height_inches": 74.0, "stats": {
        "free_throw": 84, "shot_close": 80, "mid_range": 80, "three_point": 84,
        "dunk_rating": 60, "vertical_leap": 31, "speed": 86, "stamina": 86,
        "passing_accuracy": 85, "ball_handle": 88, "physical_strength": 66, "hustle": 82,
        "perimeter_defense": 82, "interior_defense": 48, "defensive_awareness": 82,
        "rebounding": 44, "steal": 80, "block": 26}},
    {"name": "Eli Brookhart", "role": "SG", "height_inches": 78.0, "stats": {
        "free_throw": 86, "shot_close": 82, "mid_range": 84, "three_point": 88,
        "dunk_rating": 70, "vertical_leap": 34, "speed": 84, "stamina": 86,
        "passing_accuracy": 80, "ball_handle": 84, "physical_strength": 72, "hustle": 84,
        "perimeter_defense": 86, "interior_defense": 58, "defensive_awareness": 86,
        "rebounding": 56, "steal": 80, "block": 40}},
    {"name": "Darius Kohl", "role": "SF", "height_inches": 81.0, "stats": {
        "free_throw": 78, "shot_close": 86, "mid_range": 84, "three_point": 82,
        "dunk_rating": 84, "vertical_leap": 36, "speed": 82, "stamina": 88,
        "passing_accuracy": 82, "ball_handle": 84, "physical_strength": 82, "hustle": 86,
        "perimeter_defense": 88, "interior_defense": 70, "defensive_awareness": 88,
        "rebounding": 70, "steal": 78, "block": 60}},
    {"name": "Aleksei Trent", "role": "PF", "height_inches": 82.0, "stats": {
        "free_throw": 76, "shot_close": 82, "mid_range": 78, "three_point": 80,
        "dunk_rating": 80, "vertical_leap": 33, "speed": 74, "stamina": 82,
        "passing_accuracy": 76, "ball_handle": 70, "physical_strength": 84, "hustle": 84,
        "perimeter_defense": 78, "interior_defense": 84, "defensive_awareness": 86,
        "rebounding": 82, "steal": 64, "block": 78}},
    {"name": "Quentin Maddox", "role": "C", "height_inches": 84.0, "stats": {
        "free_throw": 70, "shot_close": 80, "mid_range": 62, "three_point": 50,
        "dunk_rating": 82, "vertical_leap": 32, "speed": 66, "stamina": 80,
        "passing_accuracy": 66, "ball_handle": 52, "physical_strength": 88, "hustle": 82,
        "perimeter_defense": 58, "interior_defense": 88, "defensive_awareness": 84,
        "rebounding": 88, "steal": 50, "block": 86}},
    {"name": "Reggie Sol", "role": "PG", "height_inches": 75.0, "stats": {
        "free_throw": 82, "shot_close": 74, "mid_range": 74, "three_point": 80,
        "dunk_rating": 56, "vertical_leap": 29, "speed": 80, "stamina": 80,
        "passing_accuracy": 82, "ball_handle": 82, "physical_strength": 62, "hustle": 80,
        "perimeter_defense": 76, "interior_defense": 44, "defensive_awareness": 78,
        "rebounding": 42, "steal": 74, "block": 24}},
    {"name": "Trey Halloran", "role": "SF", "height_inches": 80.0, "stats": {
        "free_throw": 78, "shot_close": 76, "mid_range": 76, "three_point": 82,
        "dunk_rating": 66, "vertical_leap": 31, "speed": 78, "stamina": 80,
        "passing_accuracy": 72, "ball_handle": 72, "physical_strength": 72, "hustle": 84,
        "perimeter_defense": 84, "interior_defense": 62, "defensive_awareness": 82,
        "rebounding": 58, "steal": 74, "block": 48}},
    {"name": "Bo Castellan", "role": "C", "height_inches": 83.0, "stats": {
        "free_throw": 66, "shot_close": 76, "mid_range": 58, "three_point": 40,
        "dunk_rating": 78, "vertical_leap": 30, "speed": 64, "stamina": 78,
        "passing_accuracy": 60, "ball_handle": 48, "physical_strength": 84, "hustle": 80,
        "perimeter_defense": 54, "interior_defense": 82, "defensive_awareness": 78,
        "rebounding": 84, "steal": 46, "block": 80}},
]


# -- DEN: offense funneled through an elite playmaking center (Nuggets-style) --
DENVER_ALTITUDE: List[PlayerData] = [
    {"name": "Niko Petrov", "role": "C", "height_inches": 83.0, "stats": {
        "free_throw": 82, "shot_close": 90, "mid_range": 84, "three_point": 70,
        "dunk_rating": 78, "vertical_leap": 28, "speed": 64, "stamina": 90,
        "passing_accuracy": 96, "ball_handle": 84, "physical_strength": 88, "hustle": 84,
        "perimeter_defense": 56, "interior_defense": 82, "defensive_awareness": 90,
        "rebounding": 92, "steal": 70, "block": 70}},
    {"name": "Jamal Reese", "role": "PG", "height_inches": 75.0, "stats": {
        "free_throw": 84, "shot_close": 82, "mid_range": 82, "three_point": 82,
        "dunk_rating": 62, "vertical_leap": 30, "speed": 84, "stamina": 86,
        "passing_accuracy": 84, "ball_handle": 86, "physical_strength": 68, "hustle": 86,
        "perimeter_defense": 80, "interior_defense": 50, "defensive_awareness": 80,
        "rebounding": 50, "steal": 78, "block": 30}},
    {"name": "Cole Whitman", "role": "SG", "height_inches": 77.0, "stats": {
        "free_throw": 80, "shot_close": 76, "mid_range": 78, "three_point": 84,
        "dunk_rating": 60, "vertical_leap": 30, "speed": 78, "stamina": 82,
        "passing_accuracy": 74, "ball_handle": 76, "physical_strength": 66, "hustle": 80,
        "perimeter_defense": 78, "interior_defense": 50, "defensive_awareness": 80,
        "rebounding": 48, "steal": 72, "block": 30}},
    {"name": "Andre Boll", "role": "SF", "height_inches": 79.0, "stats": {
        "free_throw": 74, "shot_close": 78, "mid_range": 72, "three_point": 76,
        "dunk_rating": 72, "vertical_leap": 33, "speed": 80, "stamina": 86,
        "passing_accuracy": 72, "ball_handle": 70, "physical_strength": 74, "hustle": 90,
        "perimeter_defense": 88, "interior_defense": 66, "defensive_awareness": 86,
        "rebounding": 62, "steal": 80, "block": 52}},
    {"name": "Viktor Lund", "role": "PF", "height_inches": 81.0, "stats": {
        "free_throw": 70, "shot_close": 78, "mid_range": 70, "three_point": 64,
        "dunk_rating": 76, "vertical_leap": 32, "speed": 72, "stamina": 80,
        "passing_accuracy": 68, "ball_handle": 60, "physical_strength": 82, "hustle": 82,
        "perimeter_defense": 68, "interior_defense": 80, "defensive_awareness": 78,
        "rebounding": 80, "steal": 56, "block": 70}},
    {"name": "Dontae Fisk", "role": "PG", "height_inches": 74.0, "stats": {
        "free_throw": 78, "shot_close": 72, "mid_range": 70, "three_point": 76,
        "dunk_rating": 54, "vertical_leap": 28, "speed": 82, "stamina": 80,
        "passing_accuracy": 78, "ball_handle": 80, "physical_strength": 60, "hustle": 80,
        "perimeter_defense": 74, "interior_defense": 44, "defensive_awareness": 74,
        "rebounding": 40, "steal": 72, "block": 22}},
    {"name": "Sam Orrin", "role": "SF", "height_inches": 80.0, "stats": {
        "free_throw": 72, "shot_close": 74, "mid_range": 70, "three_point": 74,
        "dunk_rating": 68, "vertical_leap": 30, "speed": 76, "stamina": 80,
        "passing_accuracy": 70, "ball_handle": 68, "physical_strength": 72, "hustle": 82,
        "perimeter_defense": 80, "interior_defense": 60, "defensive_awareness": 78,
        "rebounding": 56, "steal": 70, "block": 46}},
    {"name": "Marko Vesely", "role": "C", "height_inches": 82.0, "stats": {
        "free_throw": 64, "shot_close": 74, "mid_range": 56, "three_point": 30,
        "dunk_rating": 74, "vertical_leap": 29, "speed": 62, "stamina": 78,
        "passing_accuracy": 64, "ball_handle": 46, "physical_strength": 84, "hustle": 78,
        "perimeter_defense": 52, "interior_defense": 80, "defensive_awareness": 76,
        "rebounding": 82, "steal": 44, "block": 76}},
]


# -- OKC: young, long, athletic, swarming perimeter D + star guard ------------
OKLAHOMA_THUNDERBIRDS: List[PlayerData] = [
    {"name": "Khari Dunn", "role": "PG", "height_inches": 78.0, "stats": {
        "free_throw": 88, "shot_close": 88, "mid_range": 88, "three_point": 78,
        "dunk_rating": 72, "vertical_leap": 35, "speed": 90, "stamina": 88,
        "passing_accuracy": 84, "ball_handle": 90, "physical_strength": 70, "hustle": 86,
        "perimeter_defense": 84, "interior_defense": 56, "defensive_awareness": 86,
        "rebounding": 54, "steal": 86, "block": 48}},
    {"name": "Tobias Reyna", "role": "SG", "height_inches": 79.0, "stats": {
        "free_throw": 80, "shot_close": 78, "mid_range": 78, "three_point": 82,
        "dunk_rating": 70, "vertical_leap": 34, "speed": 86, "stamina": 86,
        "passing_accuracy": 78, "ball_handle": 80, "physical_strength": 68, "hustle": 88,
        "perimeter_defense": 88, "interior_defense": 58, "defensive_awareness": 86,
        "rebounding": 52, "steal": 88, "block": 44}},
    {"name": "Jaylen Frost", "role": "SF", "height_inches": 80.0, "stats": {
        "free_throw": 76, "shot_close": 80, "mid_range": 74, "three_point": 78,
        "dunk_rating": 80, "vertical_leap": 37, "speed": 88, "stamina": 88,
        "passing_accuracy": 74, "ball_handle": 76, "physical_strength": 74, "hustle": 90,
        "perimeter_defense": 90, "interior_defense": 66, "defensive_awareness": 86,
        "rebounding": 64, "steal": 86, "block": 62}},
    {"name": "Mason Akers", "role": "PF", "height_inches": 82.0, "stats": {
        "free_throw": 72, "shot_close": 80, "mid_range": 72, "three_point": 72,
        "dunk_rating": 82, "vertical_leap": 35, "speed": 78, "stamina": 84,
        "passing_accuracy": 70, "ball_handle": 64, "physical_strength": 80, "hustle": 88,
        "perimeter_defense": 76, "interior_defense": 84, "defensive_awareness": 84,
        "rebounding": 82, "steal": 70, "block": 80}},
    {"name": "Ivo Senga", "role": "C", "height_inches": 84.0, "stats": {
        "free_throw": 66, "shot_close": 80, "mid_range": 58, "three_point": 40,
        "dunk_rating": 84, "vertical_leap": 36, "speed": 70, "stamina": 82,
        "passing_accuracy": 62, "ball_handle": 50, "physical_strength": 84, "hustle": 88,
        "perimeter_defense": 60, "interior_defense": 90, "defensive_awareness": 86,
        "rebounding": 86, "steal": 60, "block": 92}},
    {"name": "Cory Nash", "role": "PG", "height_inches": 75.0, "stats": {
        "free_throw": 82, "shot_close": 74, "mid_range": 74, "three_point": 78,
        "dunk_rating": 58, "vertical_leap": 30, "speed": 84, "stamina": 82,
        "passing_accuracy": 80, "ball_handle": 82, "physical_strength": 62, "hustle": 84,
        "perimeter_defense": 80, "interior_defense": 46, "defensive_awareness": 80,
        "rebounding": 44, "steal": 82, "block": 28}},
    {"name": "Dre Whitfield", "role": "SG", "height_inches": 78.0, "stats": {
        "free_throw": 78, "shot_close": 76, "mid_range": 72, "three_point": 78,
        "dunk_rating": 72, "vertical_leap": 35, "speed": 86, "stamina": 84,
        "passing_accuracy": 72, "ball_handle": 76, "physical_strength": 66, "hustle": 88,
        "perimeter_defense": 86, "interior_defense": 54, "defensive_awareness": 82,
        "rebounding": 50, "steal": 84, "block": 40}},
    {"name": "Lonnie Pace", "role": "PF", "height_inches": 81.0, "stats": {
        "free_throw": 70, "shot_close": 76, "mid_range": 68, "three_point": 66,
        "dunk_rating": 76, "vertical_leap": 33, "speed": 76, "stamina": 82,
        "passing_accuracy": 66, "ball_handle": 60, "physical_strength": 78, "hustle": 86,
        "perimeter_defense": 74, "interior_defense": 78, "defensive_awareness": 80,
        "rebounding": 78, "steal": 66, "block": 70}},
]


# -- MIL: dominant downhill two-way forward + rim pressure (Bucks-style) ------
MILWAUKEE_VOLTAGE: List[PlayerData] = [
    {"name": "Goran Mensah", "role": "PF", "height_inches": 83.0, "stats": {
        "free_throw": 66, "shot_close": 92, "mid_range": 74, "three_point": 58,
        "dunk_rating": 94, "vertical_leap": 40, "speed": 88, "stamina": 92,
        "passing_accuracy": 80, "ball_handle": 80, "physical_strength": 94, "hustle": 92,
        "perimeter_defense": 80, "interior_defense": 90, "defensive_awareness": 88,
        "rebounding": 90, "steal": 72, "block": 84}},
    {"name": "Bryce Calloway", "role": "PG", "height_inches": 75.0, "stats": {
        "free_throw": 86, "shot_close": 78, "mid_range": 80, "three_point": 84,
        "dunk_rating": 58, "vertical_leap": 29, "speed": 80, "stamina": 84,
        "passing_accuracy": 84, "ball_handle": 86, "physical_strength": 74, "hustle": 80,
        "perimeter_defense": 76, "interior_defense": 48, "defensive_awareness": 80,
        "rebounding": 48, "steal": 74, "block": 28}},
    {"name": "Tre Vincent", "role": "SG", "height_inches": 78.0, "stats": {
        "free_throw": 82, "shot_close": 76, "mid_range": 78, "three_point": 82,
        "dunk_rating": 64, "vertical_leap": 31, "speed": 80, "stamina": 82,
        "passing_accuracy": 74, "ball_handle": 76, "physical_strength": 70, "hustle": 80,
        "perimeter_defense": 80, "interior_defense": 52, "defensive_awareness": 80,
        "rebounding": 50, "steal": 76, "block": 32}},
    {"name": "Will Hargrove", "role": "SF", "height_inches": 80.0, "stats": {
        "free_throw": 76, "shot_close": 74, "mid_range": 72, "three_point": 78,
        "dunk_rating": 70, "vertical_leap": 32, "speed": 78, "stamina": 84,
        "passing_accuracy": 72, "ball_handle": 70, "physical_strength": 76, "hustle": 86,
        "perimeter_defense": 84, "interior_defense": 64, "defensive_awareness": 82,
        "rebounding": 60, "steal": 76, "block": 50}},
    {"name": "Dejan Pavic", "role": "C", "height_inches": 84.0, "stats": {
        "free_throw": 68, "shot_close": 78, "mid_range": 60, "three_point": 42,
        "dunk_rating": 80, "vertical_leap": 31, "speed": 66, "stamina": 80,
        "passing_accuracy": 64, "ball_handle": 50, "physical_strength": 88, "hustle": 82,
        "perimeter_defense": 56, "interior_defense": 86, "defensive_awareness": 82,
        "rebounding": 86, "steal": 52, "block": 82}},
    {"name": "Marcus Lail", "role": "PG", "height_inches": 74.0, "stats": {
        "free_throw": 80, "shot_close": 72, "mid_range": 70, "three_point": 76,
        "dunk_rating": 54, "vertical_leap": 28, "speed": 82, "stamina": 80,
        "passing_accuracy": 80, "ball_handle": 80, "physical_strength": 62, "hustle": 82,
        "perimeter_defense": 78, "interior_defense": 44, "defensive_awareness": 76,
        "rebounding": 42, "steal": 78, "block": 24}},
    {"name": "Owen Bree", "role": "SF", "height_inches": 79.0, "stats": {
        "free_throw": 74, "shot_close": 72, "mid_range": 70, "three_point": 76,
        "dunk_rating": 66, "vertical_leap": 30, "speed": 76, "stamina": 80,
        "passing_accuracy": 70, "ball_handle": 68, "physical_strength": 72, "hustle": 82,
        "perimeter_defense": 80, "interior_defense": 60, "defensive_awareness": 78,
        "rebounding": 56, "steal": 72, "block": 44}},
    {"name": "Hank Dorsey", "role": "C", "height_inches": 82.0, "stats": {
        "free_throw": 62, "shot_close": 74, "mid_range": 54, "three_point": 28,
        "dunk_rating": 76, "vertical_leap": 30, "speed": 62, "stamina": 78,
        "passing_accuracy": 60, "ball_handle": 46, "physical_strength": 86, "hustle": 80,
        "perimeter_defense": 52, "interior_defense": 80, "defensive_awareness": 76,
        "rebounding": 82, "steal": 46, "block": 76}},
]


# -- DAL: elite shot-creating iso guard + rim-running lob big (Mavs-style) ----
DALLAS_LONE_STARS: List[PlayerData] = [
    {"name": "Luca Marchetti", "role": "PG", "height_inches": 79.0, "stats": {
        "free_throw": 80, "shot_close": 86, "mid_range": 88, "three_point": 84,
        "dunk_rating": 64, "vertical_leap": 29, "speed": 80, "stamina": 88,
        "passing_accuracy": 92, "ball_handle": 94, "physical_strength": 78, "hustle": 80,
        "perimeter_defense": 70, "interior_defense": 52, "defensive_awareness": 78,
        "rebounding": 62, "steal": 74, "block": 32}},
    {"name": "Kobe Sloan", "role": "SG", "height_inches": 76.0, "stats": {
        "free_throw": 84, "shot_close": 80, "mid_range": 78, "three_point": 80,
        "dunk_rating": 62, "vertical_leap": 30, "speed": 86, "stamina": 84,
        "passing_accuracy": 78, "ball_handle": 84, "physical_strength": 64, "hustle": 84,
        "perimeter_defense": 84, "interior_defense": 50, "defensive_awareness": 82,
        "rebounding": 46, "steal": 84, "block": 30}},
    {"name": "Pierce Dolan", "role": "SF", "height_inches": 80.0, "stats": {
        "free_throw": 76, "shot_close": 74, "mid_range": 72, "three_point": 78,
        "dunk_rating": 70, "vertical_leap": 32, "speed": 78, "stamina": 82,
        "passing_accuracy": 72, "ball_handle": 70, "physical_strength": 74, "hustle": 82,
        "perimeter_defense": 84, "interior_defense": 64, "defensive_awareness": 82,
        "rebounding": 60, "steal": 76, "block": 50}},
    {"name": "Omar Said", "role": "PF", "height_inches": 82.0, "stats": {
        "free_throw": 70, "shot_close": 80, "mid_range": 70, "three_point": 62,
        "dunk_rating": 80, "vertical_leap": 34, "speed": 76, "stamina": 82,
        "passing_accuracy": 68, "ball_handle": 62, "physical_strength": 82, "hustle": 86,
        "perimeter_defense": 72, "interior_defense": 82, "defensive_awareness": 80,
        "rebounding": 82, "steal": 60, "block": 74}},
    {"name": "Dragan Iliev", "role": "C", "height_inches": 85.0, "stats": {
        "free_throw": 62, "shot_close": 84, "mid_range": 56, "three_point": 30,
        "dunk_rating": 90, "vertical_leap": 36, "speed": 70, "stamina": 82,
        "passing_accuracy": 60, "ball_handle": 48, "physical_strength": 88, "hustle": 88,
        "perimeter_defense": 58, "interior_defense": 92, "defensive_awareness": 86,
        "rebounding": 88, "steal": 56, "block": 92}},
    {"name": "Jett Rowan", "role": "PG", "height_inches": 74.0, "stats": {
        "free_throw": 80, "shot_close": 72, "mid_range": 70, "three_point": 78,
        "dunk_rating": 54, "vertical_leap": 28, "speed": 82, "stamina": 80,
        "passing_accuracy": 78, "ball_handle": 80, "physical_strength": 60, "hustle": 80,
        "perimeter_defense": 76, "interior_defense": 44, "defensive_awareness": 76,
        "rebounding": 42, "steal": 78, "block": 24}},
    {"name": "Cam Bridger", "role": "SG", "height_inches": 78.0, "stats": {
        "free_throw": 82, "shot_close": 74, "mid_range": 74, "three_point": 84,
        "dunk_rating": 60, "vertical_leap": 30, "speed": 78, "stamina": 80,
        "passing_accuracy": 72, "ball_handle": 72, "physical_strength": 66, "hustle": 78,
        "perimeter_defense": 74, "interior_defense": 50, "defensive_awareness": 78,
        "rebounding": 46, "steal": 72, "block": 30}},
    {"name": "Ty Caldwell", "role": "PF", "height_inches": 81.0, "stats": {
        "free_throw": 68, "shot_close": 74, "mid_range": 66, "three_point": 64,
        "dunk_rating": 74, "vertical_leap": 32, "speed": 74, "stamina": 80,
        "passing_accuracy": 64, "ball_handle": 58, "physical_strength": 78, "hustle": 82,
        "perimeter_defense": 72, "interior_defense": 78, "defensive_awareness": 78,
        "rebounding": 78, "steal": 60, "block": 68}},
]


# -- MIN: elite defense, twin-tower size, rim protection (Wolves-style) -------
MINNESOTA_TUNDRA: List[PlayerData] = [
    {"name": "Andre Kovac", "role": "SG", "height_inches": 78.0, "stats": {
        "free_throw": 82, "shot_close": 84, "mid_range": 82, "three_point": 82,
        "dunk_rating": 82, "vertical_leap": 38, "speed": 86, "stamina": 86,
        "passing_accuracy": 78, "ball_handle": 84, "physical_strength": 76, "hustle": 88,
        "perimeter_defense": 88, "interior_defense": 60, "defensive_awareness": 84,
        "rebounding": 58, "steal": 84, "block": 56}},
    {"name": "Petros Anan", "role": "C", "height_inches": 85.0, "stats": {
        "free_throw": 72, "shot_close": 82, "mid_range": 72, "three_point": 70,
        "dunk_rating": 84, "vertical_leap": 34, "speed": 68, "stamina": 82,
        "passing_accuracy": 70, "ball_handle": 58, "physical_strength": 88, "hustle": 84,
        "perimeter_defense": 62, "interior_defense": 90, "defensive_awareness": 88,
        "rebounding": 88, "steal": 60, "block": 90}},
    {"name": "Ruben Stamos", "role": "C", "height_inches": 86.0, "stats": {
        "free_throw": 64, "shot_close": 80, "mid_range": 56, "three_point": 30,
        "dunk_rating": 84, "vertical_leap": 35, "speed": 66, "stamina": 84,
        "passing_accuracy": 58, "ball_handle": 46, "physical_strength": 90, "hustle": 90,
        "perimeter_defense": 60, "interior_defense": 96, "defensive_awareness": 92,
        "rebounding": 92, "steal": 62, "block": 96}},
    {"name": "Deshawn Pruitt", "role": "PG", "height_inches": 75.0, "stats": {
        "free_throw": 80, "shot_close": 78, "mid_range": 76, "three_point": 78,
        "dunk_rating": 60, "vertical_leap": 30, "speed": 86, "stamina": 86,
        "passing_accuracy": 84, "ball_handle": 86, "physical_strength": 66, "hustle": 84,
        "perimeter_defense": 82, "interior_defense": 48, "defensive_awareness": 82,
        "rebounding": 46, "steal": 84, "block": 28}},
    {"name": "Caleb Murton", "role": "SF", "height_inches": 80.0, "stats": {
        "free_throw": 76, "shot_close": 74, "mid_range": 72, "three_point": 78,
        "dunk_rating": 70, "vertical_leap": 32, "speed": 80, "stamina": 84,
        "passing_accuracy": 72, "ball_handle": 70, "physical_strength": 74, "hustle": 88,
        "perimeter_defense": 86, "interior_defense": 66, "defensive_awareness": 84,
        "rebounding": 62, "steal": 80, "block": 54}},
    {"name": "Nikolai Aas", "role": "PF", "height_inches": 82.0, "stats": {
        "free_throw": 70, "shot_close": 76, "mid_range": 70, "three_point": 68,
        "dunk_rating": 74, "vertical_leap": 31, "speed": 72, "stamina": 80,
        "passing_accuracy": 66, "ball_handle": 60, "physical_strength": 82, "hustle": 84,
        "perimeter_defense": 70, "interior_defense": 82, "defensive_awareness": 82,
        "rebounding": 80, "steal": 58, "block": 74}},
    {"name": "Jared Min", "role": "PG", "height_inches": 74.0, "stats": {
        "free_throw": 78, "shot_close": 70, "mid_range": 68, "three_point": 76,
        "dunk_rating": 52, "vertical_leap": 28, "speed": 80, "stamina": 80,
        "passing_accuracy": 78, "ball_handle": 78, "physical_strength": 60, "hustle": 82,
        "perimeter_defense": 80, "interior_defense": 44, "defensive_awareness": 78,
        "rebounding": 42, "steal": 80, "block": 24}},
    {"name": "Quincy Vale", "role": "SF", "height_inches": 79.0, "stats": {
        "free_throw": 74, "shot_close": 72, "mid_range": 70, "three_point": 76,
        "dunk_rating": 66, "vertical_leap": 30, "speed": 76, "stamina": 80,
        "passing_accuracy": 70, "ball_handle": 68, "physical_strength": 72, "hustle": 84,
        "perimeter_defense": 82, "interior_defense": 60, "defensive_awareness": 80,
        "rebounding": 56, "steal": 78, "block": 46}},
]


ROSTERS_MODERN: Dict[str, List[PlayerData]] = {
    "BOS": BOSTON_SHAMROCKS,
    "DEN": DENVER_ALTITUDE,
    "OKC": OKLAHOMA_THUNDERBIRDS,
    "MIL": MILWAUKEE_VOLTAGE,
    "DAL": DALLAS_LONE_STARS,
    "MIN": MINNESOTA_TUNDRA,
}

TEAM_NAMES_MODERN: Dict[str, str] = {
    "BOS": "Boston Shamrocks",
    "DEN": "Denver Altitude",
    "OKC": "Oklahoma Thunderbirds",
    "MIL": "Milwaukee Voltage",
    "DAL": "Dallas Lone Stars",
    "MIN": "Minnesota Tundra",
}
