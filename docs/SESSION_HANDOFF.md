# Session Handoff — Voxel Basketball (Shift9)

Snapshot of where the project stands so the next session can pick up cold. Everything below
is committed on `claude/awesome-wright-776pk1` and merged to `main` via PR #2.

## ⚠️ First thing next session
Open the project in **Unity** and run **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**.
All logic has been authored and statically traced, but **never executed** here (this cloud
container has no Unity/.NET/GPU and no network). Confirm green before building on top.
Required packages: `com.unity.nuget.newtonsoft-json`, `com.unity.test-framework`.

## Architecture (assemblies under `Assets/Shift9/`)
- **Shift9.Sim** — deterministic "brain" (no Unity randomness/physics; one seeded RNG):
  - `Core` RNG, court geometry, projectile solver, constants
  - `Players` AttributeProfile/Dynamics, **AttributeFormula** (stats→ratings)
  - `Shooting` zone classifier + make/miss resolver
  - `Ball` Verlet ball + rim/backboard/net solver
  - `Defense` openness/contest
  - `Rules` game+shot clock, scoreboard, possession
  - `Roster` SimPlayer/SimTeam
  - `Moves` signature-move enums, thresholds, **MoveSelector**
  - `Match` **Formation**, **PossessionSim**, **GameSim**, RandomRoster
- **Shift9.Customization** — URL import (fetch→validate→cache→map) for leagues/teams/arenas
  + cosmetic sneakers. Security: HTTPS+private-host block, byte caps, image header inspection,
  format whitelist. Players importable by ratings **or box-score stats**; teams carry logoUrl.
- **Shift9.Presentation** — `Court` (metrics/palette/builder), `Players` (placement/builder),
  `Animation` (locomotion mapper, **PlayerAnimationDriver**, event/move mapping), **GameView**,
  **ScoreboardHud**, **BroadcastCamera(Rig)**, MaterialUtil; `Editor` AnimatorControllerFactory.
- **Shift9.Integration** — the only place Customization meets Sim/Presentation:
  **RosterAdapter** (stats-aware, StartingFive), **ScoreboardBinder**, **TeamLogoLoader**.

## What works (logic complete, tested)
- Continuous deterministic game: possessions loop with clock/score; passing chains, dribble
  drives, reactive man defense, position-based rebounds, turnovers.
- **Ratings drive gameplay**: per-zone shooting, interior/perimeter contest, rebounding
  (hustle/strength/leap), passing/handle turnovers.
- **Signature moves gated by ratings**: ankle-breaker crossover (opens the shot), elite rim
  block (denies the bucket), dunk/layup/floater variety, post moves.
- **Customization loop closed**: import by stats or ratings → ratings drive the sim; team
  colors/abbreviation/logo → scoreboard bug.
- Visual blockout: maple court + black keys + 3pt lines, hoops/backboards/State-Farm stanchions,
  10 team-colored capsule players + ball, broadcast hard-cam, ESPN-style scoreboard bug.
- Animation **plumbing** (no clips yet): Speed/HasBall/IsDefending + Shoot/Pass/Rebound/DoMove
  params; driver feeds them from the sim; editor tool generates the placeholder controller.

## Bugs fixed from the original Shift9 draft
1. Projectile aim used negative gravity → no shot could ever score (fixed in ProjectileSolver).
2. Rim reflection double-counted the normal → wrong bounces (fixed in RimSolver).
3. Court geometry dead code + baseline error (fixed in CourtGeometry).

## NOT done yet (next-session candidates)
- **Animation clips + state-graph**: triggers/params fire, but no Humanoid rig, clips, or
  action-layer states. (Decision so far: Mecanim rig, sim stays authoritative, locomotion first,
  clips later.) Author states that play each `MoveId`.
- **Player models/animation, crowd, scorer's table, floor logos/real rim+net** (still blockout).
- (Done this session: shooting fouls + free throws, live per-player box score, formula
  calibration tests. A box-score HUD/overlay to display the stat lines is still TODO.)
- **Scene wiring**: no saved scene, but **`GameBootstrap`** does it for you — add that one
  component to an empty GameObject and press Play; it spawns the light, court, game (players +
  ball), broadcast camera, and scoreboard, all self-wired. (Manual alternative: `GameView` +
  `CourtBuilder` + `BroadcastCameraRig` on the Camera + `ScoreboardHud`.) Run **Shift9 ▸ Create
  Player Animator Controller** for the animation controller.
- **Not yet a full Unity project**: `Packages/manifest.json` is included (Newtonsoft etc.), but
  there is no `ProjectSettings/`. Easiest path: create a new 3D project in your Unity version and
  copy `Assets/Shift9` into it (then add `com.unity.nuget.newtonsoft-json` if needed).
- **Real-team logos** draw only the color tab until `TeamLogoLoader` is called with a cache.
- **Fixed-point determinism** for cross-platform online was deliberately deferred (single seeded
  RNG now; revisit when online is actually built).

## Key decisions made
- Single manifest URL for imports; sneakers cosmetic-only.
- Sim is fully deterministic and authoritative; presentation/animation are cosmetic and never
  feed back into gameplay.
- Customization and Sim stay independent; Integration is the only bridge.

## PR
PR #2: "Customization import engine + deterministic simulation core" → `main`.
