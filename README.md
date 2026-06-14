# Bball — Voxel Hoops

A real-time, playable isometric voxel basketball game in Python. **The game is
`voxel_hoops.py`** — that's the source of truth. Everything else in the repo is a
supporting system that feeds it (teams, ratings, custom-team import) or extends
it (a headless stats engine, playoff sim, and a Godot 3D client where custom
character models/animations are authored).

No duplicated code: the game imports its vector math from the engine, its court
geometry/scoring from `court_rules.py`, and its teams from the shared roster
registry — so one change propagates everywhere.

## Play it

```bash
pip install pygame
python voxel_hoops.py                        # opens the team-select menu
python voxel_hoops.py --home OKC --away MIN --no-menu
python voxel_hoops.py --import-url https://example.com/team.json
```

**Menu:** `←/→` pick HOME, `↑/↓` pick AWAY, `Enter` tip off. You control HOME.

**In game:** `WASD`/arrows move · `Space` shoot / dunk / catch · `E` strip steal
· `R` back to team select · `Esc` quit. You always control the **ball-carrier**
(or the nearest defender when the other team has it) — control switches
automatically.

**It's a real match:** full **5-on-5** with team AI (spacing, drives, passing,
man-to-man defense, steals), two-way scoring with 2- and 3-pointers by distance,
a distance/skill/contest **shot model**, a quarter clock with 4 quarters +
overtime, possession resets after makes, out-of-bounds turnovers, a team-select
menu, a final/rematch screen, and Steam achievements (first basket, dunk, clean
steal, buzzer-beater, comeback win).

**Tuning:** pace/scoring knobs live at the top of `voxel_hoops.py`
(`QUARTER_SECONDS`, `AI_SHOOT_CHANCE_*`, `AI_DUNK_CHANCE`); the shot-make curve
is `make_probability()` in `court_rules.py`. Defaults are conservative starting
points — tweak after a live playtest.

## Files

| File | Purpose |
|------|---------|
| **`voxel_hoops.py`** | **The game** — real-time isometric voxel basketball (pygame). |
| `court_rules.py` | Pygame-free court geometry + scoring rules (shared, testable). |
| `arcade_adapter.py` | Maps the shared 18-stat rosters into the game's 4-stat model. |
| `rosters.py` | Aggregator + runtime registry over every team (all eras + imported). |
| `rosters_2005.py` / `rosters_modern.py` | Team data: 2004-05 Spurs/Pistons; six fictional modern teams. |
| `team_import.py` | Import custom teams from a URL/file (validated, SSRF-guarded). |
| `nba_comprehensive_game_engine.py` | Headless deterministic stats engine (sim, box scores, telemetry). |
| `series.py` | Playoff layer: best-of-N series & brackets with persistent injuries. |
| `sim_service.py` | Stdlib HTTP/subprocess service exposing the engine to any client. |
| `godot/` | Godot 4 client (where you author 3D characters/animations). |
| `examples/team_template.json` | Schema/template for a custom team. |
| `tests/` | 91-test stdlib `unittest` suite. |

### Teams

`2005`: `SAS` Spurs · `DET` Pistons.
`modern` (fictional): `BOS` Boston Shamrocks · `DEN` Denver Altitude · `OKC`
Oklahoma Thunderbirds · `MIL` Milwaukee Voltage · `DAL` Dallas Lone Stars ·
`MIN` Minnesota Tundra. Plus any team you import from a URL.

## Import custom teams from a URL

Host a team as JSON anywhere and pull it in by key. It then works in the game
**and** in the stats engine, series, and brackets. Schema:
`examples/team_template.json`.

```bash
python team_import.py --file examples/team_template.json     # validate
python voxel_hoops.py --import-url https://example.com/team.json   # play it
```

**Security:** importing fetches a user-supplied URL server-side, so the loader
refuses non-public targets (loopback/private/link-local IPs), caps response
size, and times out. Ratings are clamped; unknown stat keys are dropped. For
trusted local dev, pass `--allow-private`.

## Supporting systems (kept & wired up)

These existed before the game became the focus and remain useful — they share
the same teams/data so nothing is duplicated:

- **Headless stats engine** (`nba_comprehensive_game_engine.py`): a deterministic,
  possession-by-possession simulator with box scores, fatigue/subs, injuries,
  and JSON-serializable play-by-play. Run a full sim:
  `python nba_comprehensive_game_engine.py`.
- **Playoffs** (`series.py`): best-of-N series (2-2-1-1-1 home court) and seeded
  brackets, with injuries that persist across games.
  `python series.py --bracket BOS,DEN,OKC,MIL,DAL,MIN,SAS,DET`.
- **Sim service** (`sim_service.py`): stdlib HTTP endpoints (`/simulate`,
  `/start`+`/possession`, `/series`, `/bracket`, `/import`) so a Godot/web client
  can drive or watch simulations. `python sim_service.py --port 8765`.
- **Godot 3D client** (`godot/`): where you build your 3D characters and
  animations. Player models are swappable via the `player_scene` export.

## Develop

```bash
python -m unittest discover -s tests -v     # 91 tests, stdlib only
```

> Roster ratings are archetype-derived approximations; the modern teams and
> their players are fictional.
