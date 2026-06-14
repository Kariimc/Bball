# Arena Scene Layout — literal map from reference screenshots

Source of truth: the five photographed `NBA 2K`-style reference frames (Knicks @ Spurs,
"NBA Finals", Frost Bank Center). This document records **only what is literally visible**
in those frames and maps it onto the coordinate frame already defined in
`Assets/Shift9/Sim/Core/SimConstants.cs`. Anything not resolvable from the images is marked
**[FLAG]** and must not be invented downstream.

> Distances on the court itself (94×50, hoop offsets, rim height) come from `SimConstants`
> (regulation), **not** measured from the photos. Anything *outside* the lines (bench depth,
> stanchion setback, camera height, table position) is **not metrically derivable** from the
> references and is given only as a relative position + **[FLAG]**.

## 1. Coordinate frame (from SimConstants)

```
x = court WIDTH    : sidelines at x = -25 .. +25
z = court LENGTH   : baselines at z = -47 .. +47
y = UP             : floor y = 0, rim y = 10
Home hoop  = (0, 10, -41.75)   Away hoop = (0, 10, +41.75)
```

Camera/bench convention used below (chosen to match the frames, see §3):
- `-x` side = **NEAR / camera sideline** (courtside seats only)
- `+x` side = **FAR sideline** (LED table, benches, coaches, scorer/broadcast)

## 2. Plan view (top-down)

```
                         FAR SIDELINE  (x = +25)         ... tiered CROWD beyond ...
        +---------- LED table: "GO GO" · "SPURSFANSHOP.COM" · "EXCLUSIVE FINALS" · "Michelob ULTRA" ----------+
        |   [ HOME BENCH (seated row) ]   [ SCORER'S / BROADCAST ]   [ AWAY BENCH (seated row) ]   <coaches stand at line>
   z=-47 BASELINE                                                                                         z=+47 BASELINE
  +------================================================================================================------+
  |stanch|                                                       .                                       |stanch|
  |+pad  |   [BLACK PAINT/KEY]            __--  center  --__               [BLACK PAINT/KEY]              |+pad  |
  |State |   white border, dashed     (   "NBA  Finals"  )      R          white border, dashed          |State |
  |Farm  |   lane lines, faint logo       --__       __--                  lane lines, faint logo        |Farm  |
  |[bkbd]|  (•)Home hoop      R                .                                 (•)Away hoop      R      |[bkbd]|
  |z=-43 |   z=-41.75                       3pt arc                               z=+41.75                |z=+43 |
  +------================================================================================================------+
   z=-47 BASELINE                                                                                         z=+47 BASELINE
        |                         courtside premium seats  ·  [ PARKER #9 fan jersey at baseline ]                 |
        +------------------------------ NEAR SIDELINE (x = -25) ------------------------------------------+
                         [ PRIMARY BROADCAST HARD-CAM — elevated, center, behind near sideline ]
                                    ... tiered CROWD beyond ...
   (•) = hoop    R = referee (on court, positions approximate)   [bkbd] = backboard   stanch = stanchion
```

## 3. Broadcast camera (what the frames are)

- Single **elevated hard-cam at center**, behind the **near (`-x`) sideline**, angled slightly
  down across the court width. Baskets read left/right; benches+table read across the top.
- This is the standard "primary" broadcast angle — **opposite the benches**. The benches,
  scorer's table and LED table are therefore on the **far (`+x`) sideline**.
- Camera height / exact setback: **[FLAG]** not derivable.

## 4. Element catalog (literal observations)

| Element | Observed in frames (literal) | Mapped position | Confidence |
|---|---|---|---|
| Hardwood | Light maple, full gloss | floor `y=0`, `±25 × ±47` | high |
| Center logo | Silver/black **"NBA Finals"** script + logoman, center circle | `(0,0,0)` | high |
| Painted key (×2) | **BLACK** paint, white border, **dashed** lane + restricted lines, faint team logo inside | each key, baseline-end | high |
| 3-pt arc / lines | Standard arc + straight corners | per `SimConstants` (ArcRadius 23.75, CornerThreeX 22) | high |
| Apron branding | "Michelob ULTRA", "Finals", NBA logoman near keys | court apron, `+x` & baseline | high |
| Backboard (×2) | Clear rectangular board on each baseline | `z=±43.0` (BackboardInset 1.25 behind hoop), board face `+/-` toward court | high (color of shooter's square **[FLAG]**) |
| Stanchion + pad (×2) | Padded base behind each backboard, **"State Farm"** red logo on front pad | behind each baseline, `|z| > 47`, exact setback **[FLAG]** | high (presence) / **[FLAG]** (setback) |
| Shot-clock | Red digits at top of frame above the boards | on top of each backboard **[FLAG: could be arena fascia]** | **[FLAG]** |
| Referees | Dark/black uniforms on court; a ref by the lane bears **#39**; others mid-court | on-court, `R` marks above; **count per frame is partial** | medium; exact count **[FLAG]** |
| Coaches | Figures **standing** at the far (`+x`) sideline, dark attire, in front of bench | line `x≈+25`, near mid-court | medium |
| Bench players | **Seated row** on far (`+x`) sideline behind LED table — blue warmups (NY) & white (SA) | `x` just beyond `+25`, split L/R **[FLAG: which team which end]** | medium |
| Scorer's / broadcast table | LED table mid-court far side cycling ads ("GO GO", "SPURSFANSHOP.COM", "EXCLUSIVE FINALS") | `x≈+25`, `z≈0` | medium |
| Arena ID | **"Frost Bank Center"** on far wall | far-side upper wall | high |
| Crowd | Dense tiered stands both sidelines, stair vomitories, "NBA Finals" wall logos | beyond both sidelines | high |
| ESPN scorebug | `NY 26 · SA 35 · 2ND 4:18 · shot clk · NY LEADS 3-1` | **2D overlay, not world geometry** | n/a |

## 5. Open ambiguities — DO NOT GUESS (flagged for the user)

1. **Shooter's-square / backboard branding color** — not legible.
2. **Red digit blocks at frame top** — shot-clock-on-backboard vs. arena fascia scoreboard: unresolved.
3. **Stanchion setback** behind baseline and **camera height** — not metrically derivable from photos.
4. **Bench assignment** — which team's bench sits at the `-z` vs `+z` end is not consistent/legible across frames.
5. **Referee count/positions** — only a partial subset is visible in any single frame (#39 by the lane confirmed).
6. **Broadcast booth** — distinct commentary booth vs. the scorer's LED table is not separable in the frames; treated as co-located mid-table on the far side until clarified.
