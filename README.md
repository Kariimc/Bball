# Bball

A deterministic, fully-playable NBA game-simulation engine in pure Python
(stdlib only). Designed as the headless logic layer for a 2.5D / voxel
basketball game — it computes pure game-state telemetry and never renders, so
visuals and animation can be driven by a separate front end (e.g. Ursina).

Season lens: **2004-05 NBA Finals — San Antonio Spurs vs. Detroit Pistons.**

## Files

| File | Purpose |
|------|---------|
| `nba_comprehensive_game_engine.py` | The engine: vector math, entities, AI, possession + full-game sim, injuries, steppable API. |
| `rosters_2005.py` | 2004-05 Spurs & Pistons (archetype-approximated ratings). |
| `rosters_modern.py` | Six fictional teams modeled on current-NBA play-style archetypes. |
| `rosters.py` | Aggregator: one lookup over every team across eras. |
| `series.py` | Playoff layer: best-of-N series & single-elimination brackets with persistent injuries. |
| `sim_service.py` | Stdlib HTTP / subprocess transport (replay, live-step sessions, series, bracket). |
| `godot/` | Runnable Godot 4 front-end POC (`project.godot`, `Main.tscn`, `SimClient.gd`, `Scoreboard.gd`). |
| `tests/` | 54-test regression suite (stdlib `unittest`). |

### Teams

`2005`: `SAS` Spurs · `DET` Pistons.
`modern` (fictional, archetype-based): `BOS` Boston Shamrocks · `DEN` Denver
Altitude · `OKC` Oklahoma Thunderbirds · `MIL` Milwaukee Voltage · `DAL` Dallas
Lone Stars · `MIN` Minnesota Tundra.

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

## Playoffs (series, brackets, injuries)

```bash
# Best-of-7 series (injuries persist across games; higher seed hosts 2-2-1-1-1)
python series.py --a OKC --b BOS --best-of 7 --seed 7

# Single-elimination bracket (power-of-two field, seeded by team strength)
python series.py --bracket BOS,DEN,OKC,MIL,DAL,MIN,SAS,DET --seed 7

python series.py --list   # show all team keys
```

Injuries are modeled in every game: risk scales with fatigue and falls with
conditioning. A hurt player is subbed out and may miss future games
(`games_out`), which `series.py` carries forward across the series. Tune via the
engine's `injury_rate` (default `0.0005`, ~0.5 in-game injuries/game).

## Two ways for a front end to consume it

**1. Replay (deterministic, simplest).** Fetch a whole game, animate the
`play_by_play` array at your own pace.

**2. Live step-through (stateful session).** Drive it one possession at a time —
the engine exposes `start_game()` + `step_possession()`, surfaced over HTTP as
`/start` → repeated `/possession`.

```python
engine.start_game()
while True:
    play = engine.step_possession()
    if play["game_over"]:
        break
    render(play)   # play["clock"], play["score"], play["events"], play["shot"]
```

## Front-end integration (Godot via Python sim service)

The engine is headless Python; Godot renders. Because every game is
deterministic, the pattern is **fetch once, replay locally** — Godot pulls a
full game and animates the `play_by_play` array at its own pace, driving the
scoreboard/clock from the telemetry fields. No per-frame round trips.

```bash
# Start the sim service (stdlib only, no Flask/FastAPI)
python sim_service.py --port 8765

#   GET /health
#   GET /teams
#   GET /simulate?seed=2005&difficulty=0.7&home=SAS&away=DET   (replay mode)
#   GET /start?seed=7&home=BOS&away=DEN  ->  GET /possession?session=ID  (live step)
#   GET /series?a=BOS&b=DEN&best_of=7
#   GET /bracket?teams=BOS,DEN,OKC,MIL,DAL,MIN,SAS,DET

# Or one-shot subprocess mode (Godot OS.execute):
python sim_service.py --once --seed 7 --difficulty 0.7
```

**Runnable Godot POC:** with the service running, open the `godot/` folder in
Godot 4 and press Play — `Main.tscn` drives a live scoreboard + play feed via
`/start` → `/possession`. `SimClient.gd` supports both replay and live-step
modes; `Scoreboard.gd` shows how to read the telemetry:

```gdscript
@onready var sim: SimClient = $Sim
sim.session_started.connect(func(info): timer.start())   # live-step
sim.possession_played.connect(func(play):
    $HomeScore.text = str(play["score"]["HOME"])
    $Clock.text = play.get("clock", ""))
sim.start_session(7, 0.6, "BOS", "DEN")
# then call sim.step() on a Timer to advance one possession at a time
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
