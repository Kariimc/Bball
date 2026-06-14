# ==============================================================================
#                          TESTS / TEST_ARCADE.PY
# ==============================================================================
# Tests for the arcade adapter -- the bridge that feeds the shared 18-stat
# rosters (2005 / modern / URL-imported) into the real-time game's 4-stat model.
# Pygame-free, so the data path is verifiable without a display.
# ==============================================================================

from __future__ import annotations
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from arcade_adapter import (  # noqa: E402
    to_arcade_stats, build_arcade_team, get_arcade_matchup, ARCADE_KEYS,
)
from rosters import all_teams  # noqa: E402
from team_import import load_team_data_from_file, register_imported_team  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
TEMPLATE = os.path.join(os.path.dirname(HERE), "examples", "team_template.json")


class TestStatMapping(unittest.TestCase):
    def test_all_keys_present_and_in_range(self):
        s = to_arcade_stats({"speed": 94, "vertical_leap": 40, "steal": 86,
                             "three_point": 80, "mid_range": 80, "shot_close": 83})
        self.assertEqual(set(s.keys()), set(ARCADE_KEYS))
        self.assertTrue(all(1 <= v <= 10 for v in s.values()))

    def test_defaults_fill_missing_ratings(self):
        s = to_arcade_stats({})  # nothing provided
        self.assertTrue(all(1 <= v <= 10 for v in s.values()))

    def test_elite_outranks_scrub(self):
        elite = to_arcade_stats({"speed": 95, "steal": 90})
        scrub = to_arcade_stats({"speed": 40, "steal": 35})
        self.assertGreater(elite["speed"], scrub["speed"])
        self.assertGreater(elite["steal"], scrub["steal"])

    def test_vertical_from_inches(self):
        low = to_arcade_stats({"vertical_leap": 20})
        high = to_arcade_stats({"vertical_leap": 44})
        self.assertLess(low["vertical"], high["vertical"])
        self.assertLessEqual(high["vertical"], 10)


class TestTeamBuilding(unittest.TestCase):
    def test_every_team_builds_sorted(self):
        for key in all_teams():
            team = build_arcade_team(key)
            self.assertGreaterEqual(len(team), 5)
            overalls = [p["overall"] for p in team]
            self.assertEqual(overalls, sorted(overalls, reverse=True))
            for p in team:
                self.assertIn("name", p)
                self.assertEqual(set(p["stats"].keys()), set(ARCADE_KEYS))

    def test_matchup_returns_names_and_players(self):
        hn, hp, an, ap = get_arcade_matchup("BOS", "DEN")
        self.assertEqual(hn, "Boston Shamrocks")
        self.assertEqual(an, "Denver Altitude")
        self.assertGreaterEqual(len(hp), 5)
        self.assertGreaterEqual(len(ap), 5)

    def test_imported_team_flows_into_arcade(self):
        register_imported_team(load_team_data_from_file(TEMPLATE, key="ARCT"))
        team = build_arcade_team("ARCT")
        self.assertGreaterEqual(len(team), 5)
        self.assertEqual(set(team[0]["stats"].keys()), set(ARCADE_KEYS))


if __name__ == "__main__":
    unittest.main(verbosity=2)
