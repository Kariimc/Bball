# Bball — Voxel Hoops

A real-time, playable isometric voxel basketball game in Python. **The game is
`voxel_hoops.py`** — that's the source of truth. Everything else in the repo is a
supporting system that feeds it (teams, ratings, custom-team import) or extends
it (a headless stats engine, playoff sim, and a Godot 3D client where custom
character models/animations are authored).

No duplicated code: the game imports its vector math from the engine, its court
geometry/scoring from `court_rules.py`, and its teams from the shared roster
registry — so one change propagates everywhere.

## Play on your phone (no install, no server)

`web/bball.html` is a **single self-contained file** — the whole game in one
HTML file, no downloads, no internet, no server. Put it on your Android phone
and tap it; it opens in your browser and plays offline. Easiest ways to get it
onto the phone:

- Email `web/bball.html` to yourself and open the attachment in Chrome, **or**
- Save it to your phone's Downloads and tap it in the Files app (opens in browser), **or**
- Open it on any device and tap **⇪ SHARE** on the menu to send the file to your
  phone (or a friend) via any messaging app; **⭳ SAVE** keeps a copy. After it's
  open on the phone, use the browser's "Add to Home Screen" to relaunch like an app.

Turn the phone **sideways (landscape)**. Drag the **left** side to move; the
**SHOOT / PASS / STEAL** buttons are on the right. Tap a half of the menu to pick
teams, then TIP OFF. You always control the green (HOME) ball-handler. The full
cast is on screen and reacts: crowd + momentum meter, mascot, cheerleaders,
coaches (flash when they argue a call), referees (flash on a whistle), and a
camera that flashes on big baskets. Same teams/players as the desktop game.

> This web build is a standalone version (phones can't run the Python directly
> without a server). The desktop `voxel_hoops.py` remains the full game.

## Play it (desktop)

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

The desktop game now also renders the **broadcast cast** (crowd + momentum,
referees, mascot, cheerleaders, coaches that flash when they argue a foul, plus a
celebration screen flash) — the same `presentation.py` logic the web build uses.

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
| `game_events.py` | Tiny event bus: gameplay emits, presentation/officiating react. |
| `presentation.py` | Crowd, mascot, cheerleaders, broadcast camera, benches, coaches. |
| `officiating.py` | Referee AI, real-time violations/fouls, jump-ball kinematics, free-throw lanes. |
| `sim_service.py` | Stdlib HTTP/subprocess service exposing the engine to any client. |
| `godot/` | Godot 4 client (where you author 3D characters/animations). |
| `examples/team_template.json` | Schema/template for a custom team. |
| `tests/` | 125-test stdlib `unittest` suite. |

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

## Presentation & officiating (entities, ready for your models)

Beyond the players, the full broadcast cast is simulated as **model-agnostic
entities** that react to gameplay through `game_events.py` and emit serializable
telemetry + *animation intents* — a renderer reads those and plays the matching
clip on your 3D model. Nothing here depends on a renderer.

- **Officiating** (`officiating.py`): `RefereeAI` (walks to the jump ball,
  retrieves loose balls, hands off), `LiveOfficiatingEngine` (shot clock,
  backcourt, closely-guarded, 3-second, traveling, double-dribble, carry, fouls
  + bonus + last-two-minutes, technicals), `JumpBallSimulation` (gravity-true
  tip + timing windows), `FreeThrowSystem` (lane placement + sequence).
- **Presentation** (`presentation.py`): `DynamicCrowdSystem` (crowd states +
  0-1 momentum), `EntertainmentManager` (mascot, cheerleaders, halftime show,
  t-shirt cannon), `CameraDirector` (tracks ball, cuts to celebrations/crowd),
  and `SidelineUnit` per team (bench mob + coach states: pacing, arguing,
  timeout huddle, ejected).

Both are wired into `NBAUnifiedEngine`: every game emits SCORE/DUNK/FOUL/
PERIOD_END/INJURY/… events, the crowd/camera/benches/coaches react, and the
result is exposed two ways — a compact `broadcast` snapshot on every possession,
and the full `broadcast` block in the game summary.

**Attaching your models/animations:** every athlete (`EntityProxy`) and every
presentation `Actor` exposes `play_anim()`, `play_loop()`,
`trigger_one_shot_action()`, and a `current_anim` / `active_animations` field.
Your Godot (or other) renderer reads the entity's position + `current_anim` and
drives the attached model — the names are the contract, the model is yours.

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
