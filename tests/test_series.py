# ==============================================================================
#                          TESTS / TEST_SERIES.PY
# ==============================================================================
# Regression suite for the playoff layer: modern rosters, steppable possession
# API, injuries (in-game + cross-game persistence), best-of-N series, and
# single-elimination brackets. Pure stdlib unittest, deterministic via seeds.
# ==============================================================================

from __future__ import annotations
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from nba_comprehensive_game_engine import NBAUnifiedEngine, build_team  # noqa: E402
from rosters import ALL_ROSTERS, get_roster, team_name, all_teams  # noqa: E402
from rosters_modern import ROSTERS_MODERN  # noqa: E402
from series import (  # noqa: E402
    simulate_series, simulate_bracket, team_strength, _home_pattern,
    _recover_between_games, make_team,
)


# ------------------------------------------------------------- modern rosters


class TestModernRosters(unittest.TestCase):
    def test_all_modern_teams_build_with_full_lineups(self):
        for key in ROSTERS_MODERN:
            team = build_team(team_name(key), "HOME", get_roster(key))
            self.assertGreaterEqual(len(team.roster), 8)
            self.assertEqual(len(team.on_court), 5)
            self.assertEqual({p.role for p in team.on_court}, {"PG", "SG", "SF", "PF", "C"})

    def test_aggregator_merges_both_eras(self):
        self.assertIn("SAS", all_teams())   # 2005
        self.assertIn("BOS", all_teams())   # modern
        self.assertEqual(len(ALL_ROSTERS), len(all_teams()))

    def test_unknown_team_raises(self):
        with self.assertRaises(KeyError):
            get_roster("XYZ")

    def test_teams_are_roughly_balanced(self):
        strengths = [team_strength(k) for k in ROSTERS_MODERN]
        self.assertLess(max(strengths) - min(strengths), 10.0)  # no runaway team


# -------------------------------------------------------------- steppable API


class TestSteppableEngine(unittest.TestCase):
    def _teams(self):
        return (build_team("BOS", "HOME", get_roster("BOS")),
                build_team("DEN", "AWAY", get_roster("DEN")))

    def test_step_matches_full_run(self):
        h, a = self._teams()
        e1 = NBAUnifiedEngine(home=h, away=a, seed=11)
        e1.start_game()
        while not e1.step_possession().get("game_over"):
            pass
        h2, a2 = self._teams()
        e2 = NBAUnifiedEngine(home=h2, away=a2, seed=11)
        e2.simulate_game()
        self.assertEqual(e1.game_state.score, e2.game_state.score)

    def test_game_over_payload_carries_summary(self):
        h, a = self._teams()
        e = NBAUnifiedEngine(home=h, away=a, seed=4)
        e.start_game()
        result = {}
        for _ in range(2000):
            result = e.step_possession()
            if result.get("game_over"):
                break
        self.assertTrue(result["game_over"])
        self.assertIn("summary", result)
        self.assertIn("final_score", result["summary"])

    def test_step_after_game_over_is_idempotent(self):
        h, a = self._teams()
        e = NBAUnifiedEngine(home=h, away=a, seed=4)
        e.simulate_game()  # drives stepper internally to completion
        again = e.step_possession()
        self.assertTrue(again["game_over"])


# ------------------------------------------------------------------- injuries


class TestInjuries(unittest.TestCase):
    def test_high_rate_produces_recorded_injuries(self):
        total = 0
        for s in range(20):
            e = NBAUnifiedEngine(home=build_team("BOS", "HOME", get_roster("BOS")),
                                 away=build_team("DEN", "AWAY", get_roster("DEN")),
                                 seed=s, injury_rate=0.002)
            g = e.simulate_game()
            total += len(g["injuries"])
            for inj in g["injuries"]:
                self.assertIn("player", inj)
                self.assertIn("note", inj)
                self.assertGreaterEqual(inj["games_out"], 0)
        self.assertGreater(total, 0)

    def test_extreme_rate_does_not_crash(self):
        # The forfeit guard must keep a wiped-out roster from raising.
        e = NBAUnifiedEngine(home=build_team("BOS", "HOME", get_roster("BOS")),
                             away=build_team("DEN", "AWAY", get_roster("DEN")),
                             seed=1, injury_rate=0.02)
        g = e.simulate_game()
        self.assertIn("final_score", g)

    def test_zero_rate_means_no_injuries(self):
        e = NBAUnifiedEngine(home=build_team("BOS", "HOME", get_roster("BOS")),
                             away=build_team("DEN", "AWAY", get_roster("DEN")),
                             seed=5, injury_rate=0.0)
        self.assertEqual(e.simulate_game()["injuries"], [])

    def test_recovery_ticks_down_and_returns_player(self):
        team = make_team("BOS")
        p = team.roster[0]
        p.available = False
        p.games_out = 2
        _recover_between_games(team)  # miss one more
        self.assertFalse(p.available)
        self.assertEqual(p.games_out, 1)
        _recover_between_games(team)  # still out (games_out 1 -> 0)
        self.assertFalse(p.available)
        _recover_between_games(team)  # now returns
        self.assertTrue(p.available)


# -------------------------------------------------------------------- series


class TestSeries(unittest.TestCase):
    def test_home_patterns(self):
        self.assertEqual(_home_pattern(7), [True, True, False, False, True, False, True])
        self.assertEqual(len(_home_pattern(5)), 5)

    def test_series_has_a_winner_with_enough_wins(self):
        s = simulate_series("BOS", "DEN", best_of=7, seed=3)
        needed = 4
        self.assertEqual(s["wins"][s["winner"]], needed)
        self.assertLessEqual(len(s["games"]), 7)
        # series stops as soon as a team clinches
        self.assertGreaterEqual(len(s["games"]), needed)

    def test_series_is_deterministic(self):
        a = simulate_series("OKC", "MIL", best_of=5, seed=8)
        b = simulate_series("OKC", "MIL", best_of=5, seed=8)
        self.assertEqual(a["wins"], b["wins"])
        self.assertEqual([g["score"] for g in a["games"]],
                         [g["score"] for g in b["games"]])

    def test_each_game_has_a_decisive_score(self):
        s = simulate_series("MIN", "DAL", best_of=5, seed=2)
        for g in s["games"]:
            self.assertNotEqual(g["score"]["HOME"], g["score"]["AWAY"])


# -------------------------------------------------------------------- bracket


class TestBracket(unittest.TestCase):
    def test_bracket_crowns_a_champion_from_the_field(self):
        keys = ["BOS", "DEN", "OKC", "MIN"]
        b = simulate_bracket(keys, seed=7, best_of=5)
        names = {team_name(k) for k in keys}
        self.assertIn(b["champion"], names)
        self.assertEqual(len(b["rounds"]), 2)  # 4 teams -> 2 rounds

    def test_bracket_requires_power_of_two(self):
        with self.assertRaises(ValueError):
            simulate_bracket(["BOS", "DEN", "OKC"], seed=1)

    def test_eight_team_bracket_runs(self):
        keys = ["BOS", "DEN", "OKC", "MIL", "DAL", "MIN", "SAS", "DET"]
        b = simulate_bracket(keys, seed=1, best_of=3)
        self.assertEqual(len(b["rounds"]), 3)  # 8 -> 4 -> 2 -> 1
        self.assertIn(b["champion"], {team_name(k) for k in keys})


if __name__ == "__main__":
    unittest.main(verbosity=2)
