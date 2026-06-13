# Bball

A deterministic, fully-playable NBA game-simulation engine in pure Python
(stdlib only). Designed as the headless logic layer for a 2.5D / voxel
basketball game — it computes pure game-state telemetry and never renders, so
visuals and animation can be driven by a separate front end (e.g. Ursina).

Season lens: **2004-05 NBA Finals — San Antonio Spurs vs. Detroit Pistons.**

## Files

| File | Purpose |
|------|---------|
| `nba_comprehensive_game_engine.py` | The engine: enums, vector math, entities, AI, possession + full-game simulation. |
| `rosters_2005.py` | Pure roster data for the 2004-05 Spurs & Pistons (archetype-approximated ratings). |
| `tests/test_engine.py` | 36-test regression suite (stdlib `unittest`). |

## Quick start

```bash
# Simulate a full game with live play-by-play and box scores
python nba_comprehensive_game_engine.py

# Run the test suite
python -m unittest discover -s tests -v
```

### Use it as a library

```python
from nba_comprehensive_game_engine import NBAUnifiedEngine, build_team
from rosters_2005 import SPURS_2005, PISTONS_2005

home = build_team("San Antonio Spurs", "HOME", SPURS_2005)
away = build_team("Detroit Pistons", "AWAY", PISTONS_2005)

engine = NBAUnifiedEngine(home=home, away=away, seed=2005, difficulty=0.7)
summary = engine.simulate_game(verbose=False)

print(summary["final_score"])          # {'HOME': ..., 'AWAY': ...}
print(summary["home"]["players"][0])   # leading scorer's full box line
# engine.play_by_play -> list of serializable possession telemetry dicts
```

## Design

- **Deterministic.** A single seeded `random.Random` is threaded through every
  stochastic system, so `seed=N` always reproduces the exact same game — ideal
  for replays, debugging, and data migration.
- **Single source of truth.** `GameState` owns the clock, score, fouls, and
  possession; `GameScenarioEngine` is a pure strategy advisor that *reads* it
  (no duplicated, drifting state).
- **Serializable telemetry.** Every possession and the final summary export to
  plain dicts (`json.dumps`-safe) — rendering layers consume telemetry, never
  internal objects.
- **No heavy dependencies.** Custom `Vector3` (slots), linear-interpolated AI
  envelopes, and probabilistic shot curves — no NumPy, no engine lock-in.

### Simulated systems

Jump-ball tip-off · possession-by-possession half-court & transition offense ·
shot selection by zone/play/strategy · defensive contests, paint denial &
shot-blocking · live-ball turnovers → fast breaks · rebounding & second-chance
putbacks · shooting fouls, and-1s, free throws & the penalty/bonus ·
crunch-time tactics (clock-kill iso, three-hunting, intentional fouls) ·
fatigue + automatic substitutions · 4 quarters with overtime.

### Tuning

`difficulty` (0.0–1.0) scales AI reaction/timing windows. Shot realism is
governed by `SHOT_CURVE` (rating → expected FG% per zone). Out of the box the
engine produces NBA-plausible lines: ~100 ppg, ~44% FG, ~32% 3P, ~200
possessions/game, balanced win split between evenly-matched teams.

> Roster ratings are archetype-derived approximations tuned to reproduce each
> player's *relative* real-world strengths that season, not official numbers.
