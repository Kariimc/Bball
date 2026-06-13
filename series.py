# ==============================================================================
#                                  SERIES.PY
# ==============================================================================
# Playoff layer on top of the game engine: best-of-N series and single-
# elimination brackets, with injuries that PERSIST across games (a player hurt
# in Game 2 can miss the rest of the series).
#
# Home court matters: the higher seed hosts under the standard 2-2-1-1-1 format,
# and the engine applies a home-court shooting edge, so swapping venues changes
# outcomes. Teams are built once and reused across games (reset per game) so
# injury availability carries forward.
#
# CLI:
#   python series.py --a SAS --b DET --best-of 7 --seed 2005
#   python series.py --bracket BOS,DEN,OKC,MIL,DAL,MIN,SAS,DET --seed 7
# ==============================================================================

from __future__ import annotations
import argparse
from typing import Any, Dict, List, Optional, Tuple

from nba_comprehensive_game_engine import NBAUnifiedEngine, Team, build_team
from rosters import get_roster, team_name, all_teams


# ----------------------------------------------------------- helpers


def make_team(key: str) -> Team:
    """Build a persistent Team (side is rebound per game by the series loop)."""
    return build_team(team_name(key), "HOME", get_roster(key))


def team_strength(key: str) -> float:
    """Top-8 average overall -- used to seed brackets realistically."""
    team = make_team(key)
    ovr = sorted((p.overall() for p in team.roster), reverse=True)
    return sum(ovr[:8]) / min(8, len(ovr))


def _home_pattern(best_of: int) -> List[bool]:
    """True => higher seed hosts. Standard NBA formats; alternate otherwise."""
    if best_of == 7:
        return [True, True, False, False, True, False, True]
    if best_of == 5:
        return [True, True, False, False, True]
    if best_of == 3:
        return [True, False, True]
    return [i % 2 == 0 for i in range(best_of)]


def _recover_between_games(team: Team) -> None:
    """Tick down injury availability before a new game."""
    for p in team.roster:
        if not p.available:
            if p.games_out > 0:
                p.games_out -= 1
            else:
                p.available = True
                p.injury_note = ""


# ----------------------------------------------------------- series


def simulate_series(team_a_key: str, team_b_key: str, *, best_of: int = 7,
                    seed: Optional[int] = None, difficulty: float = 0.6,
                    injury_rate: float = 0.0006,
                    a_is_higher_seed: bool = True) -> Dict[str, Any]:
    """Simulate a best-of-N series. `team_a` is the higher seed by default
    (hosts per the home pattern). Injuries persist across games."""
    a = make_team(team_a_key)
    b = make_team(team_b_key)
    name_a, name_b = a.name, b.name
    needed = best_of // 2 + 1
    pattern = _home_pattern(best_of)

    wins = {name_a: 0, name_b: 0}
    games: List[Dict[str, Any]] = []
    series_injuries: List[Dict[str, Any]] = []

    for gi in range(best_of):
        if wins[name_a] >= needed or wins[name_b] >= needed:
            break

        # Recover between games (skip before game 1).
        if gi > 0:
            _recover_between_games(a)
            _recover_between_games(b)

        higher_home = pattern[gi] if a_is_higher_seed else not pattern[gi]
        home_team, away_team = (a, b) if higher_home else (b, a)
        home_team.side, away_team.side = "HOME", "AWAY"
        home_team.reset_for_new_game()
        away_team.reset_for_new_game()

        game_seed = None if seed is None else seed * 1000 + gi
        engine = NBAUnifiedEngine(home=home_team, away=away_team, seed=game_seed,
                                  difficulty=difficulty, injury_rate=injury_rate)
        summary = engine.simulate_game()

        winner_name = summary["winner"]
        wins[winner_name] += 1
        series_injuries.extend(summary["injuries"])
        games.append({
            "game": gi + 1,
            "home": home_team.name,
            "away": away_team.name,
            "score": summary["final_score"],
            "winner": winner_name,
            "series_after": dict(wins),
            "injuries": [f"{i['player']} ({i['note']}, out {i['games_out']}g)"
                         for i in summary["injuries"]],
        })

    champion = name_a if wins[name_a] > wins[name_b] else name_b
    return {
        "type": "series",
        "best_of": best_of,
        "matchup": [name_a, name_b],
        "wins": wins,
        "winner": champion,
        "result": f"{wins[champion]}-{min(wins.values())}",
        "games": games,
        "series_injuries_count": len(series_injuries),
    }


# ----------------------------------------------------------- bracket


def simulate_bracket(team_keys: List[str], *, seed: Optional[int] = None,
                     difficulty: float = 0.6, best_of: int = 7,
                     injury_rate: float = 0.0006) -> Dict[str, Any]:
    """Single-elimination bracket. Requires a power-of-two field; seeds it by
    team strength (1 vs N, 2 vs N-1, ...). Returns every round's series."""
    n = len(team_keys)
    if n < 2 or (n & (n - 1)) != 0:
        raise ValueError(f"bracket needs a power-of-two field, got {n}")

    seeded = sorted(team_keys, key=team_strength, reverse=True)
    seeds = {k: i + 1 for i, k in enumerate(seeded)}

    rounds: List[Dict[str, Any]] = []
    alive = list(seeded)
    round_num = 0
    while len(alive) > 1:
        round_num += 1
        # Standard pairing: best vs worst remaining.
        pairings: List[Tuple[str, str]] = [
            (alive[i], alive[len(alive) - 1 - i]) for i in range(len(alive) // 2)
        ]
        round_results = []
        winners: List[str] = []
        for hi, lo in pairings:
            s = simulate_series(hi, lo, best_of=best_of,
                                seed=(None if seed is None else seed + round_num * 17 + seeds[hi]),
                                difficulty=difficulty, injury_rate=injury_rate)
            win_key = hi if s["winner"] == team_name(hi) else lo
            winners.append(win_key)
            round_results.append({
                "matchup": f"({seeds[hi]}) {team_name(hi)} vs ({seeds[lo]}) {team_name(lo)}",
                "winner": s["winner"], "result": s["result"],
            })
        rounds.append({"round": round_num, "series": round_results})
        # Re-seed survivors by original seed so the bracket stays coherent.
        alive = sorted(winners, key=lambda k: seeds[k])

    champion_key = alive[0]
    return {
        "type": "bracket",
        "field": [f"({seeds[k]}) {team_name(k)}" for k in seeded],
        "best_of": best_of,
        "rounds": rounds,
        "champion": team_name(champion_key),
    }


# ----------------------------------------------------------- CLI / print


def _print_series(s: Dict[str, Any]) -> None:
    print(f"\n=== BEST-OF-{s['best_of']}: {s['matchup'][0]} vs {s['matchup'][1]} ===")
    for g in s["games"]:
        fs = g["score"]
        line = (f"  Game {g['game']}: {g['away']} {fs['AWAY']} @ {g['home']} {fs['HOME']}"
                f"  -> {g['winner']}  (series {g['series_after'][s['matchup'][0]]}"
                f"-{g['series_after'][s['matchup'][1]]})")
        print(line)
        for inj in g["injuries"]:
            print(f"        injury: {inj}")
    print(f"  SERIES WINNER: {s['winner']} ({s['result']})")


def _print_bracket(b: Dict[str, Any]) -> None:
    print("\n=== PLAYOFF BRACKET ===")
    print("Field: " + ", ".join(b["field"]))
    for rd in b["rounds"]:
        print(f"\n-- Round {rd['round']} --")
        for ser in rd["series"]:
            print(f"  {ser['matchup']}  ->  {ser['winner']} ({ser['result']})")
    print(f"\nCHAMPION: {b['champion']}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Bball playoff simulator")
    parser.add_argument("--a", help="higher-seed team key (series mode)")
    parser.add_argument("--b", help="lower-seed team key (series mode)")
    parser.add_argument("--best-of", type=int, default=7)
    parser.add_argument("--seed", type=int, default=None)
    parser.add_argument("--difficulty", type=float, default=0.6)
    parser.add_argument("--injury-rate", type=float, default=0.0006)
    parser.add_argument("--bracket", help="comma-separated power-of-two team keys")
    parser.add_argument("--list", action="store_true", help="list available teams")
    args = parser.parse_args()

    if args.list:
        for k, v in all_teams().items():
            print(f"  {k:5s} {v}")
        return

    if args.bracket:
        keys = [k.strip().upper() for k in args.bracket.split(",")]
        _print_bracket(simulate_bracket(keys, seed=args.seed, difficulty=args.difficulty,
                                        best_of=args.best_of, injury_rate=args.injury_rate))
    elif args.a and args.b:
        _print_series(simulate_series(args.a.upper(), args.b.upper(), best_of=args.best_of,
                                      seed=args.seed, difficulty=args.difficulty,
                                      injury_rate=args.injury_rate))
    else:
        parser.error("provide --a/--b for a series, or --bracket for a bracket")


if __name__ == "__main__":
    main()
