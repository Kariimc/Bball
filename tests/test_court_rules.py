# ==============================================================================
#                        TESTS / TEST_COURT_RULES.PY
# ==============================================================================
# Tests for the pygame-free court rules that the game's scoring depends on.
# ==============================================================================

from __future__ import annotations
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from court_rules import (  # noqa: E402
    point_value, is_scoring_position, in_bounds, format_clock,
    target_hoop, HOME_HOOP, AWAY_HOOP, COURT_LENGTH, COURT_WIDTH,
    THREE_POINT_DIST, RIM_Z_LOW, RIM_Z_HIGH,
    make_probability, offensive_spot,
)


class TestTargets(unittest.TestCase):
    def test_teams_attack_opposite_hoops(self):
        self.assertEqual(target_hoop("home"), HOME_HOOP)
        self.assertEqual(target_hoop("away"), AWAY_HOOP)
        self.assertNotEqual(HOME_HOOP, AWAY_HOOP)


class TestPointValue(unittest.TestCase):
    def test_close_is_two(self):
        hx, hy = HOME_HOOP
        self.assertEqual(point_value(hx - 3, hy, "home"), 2)

    def test_deep_is_three(self):
        hx, hy = HOME_HOOP
        self.assertEqual(point_value(hx - (THREE_POINT_DIST + 2), hy, "home"), 3)

    def test_boundary_exactly_three(self):
        hx, hy = HOME_HOOP
        self.assertEqual(point_value(hx - THREE_POINT_DIST, hy, "home"), 3)

    def test_each_team_relative_to_own_hoop(self):
        # Same x can be a two for one team and a three for the other.
        ax, ay = AWAY_HOOP
        self.assertEqual(point_value(ax + 2, ay, "away"), 2)
        self.assertEqual(point_value(ax + 2, ay, "home"), 3)


class TestScoringPosition(unittest.TestCase):
    def test_in_cylinder_scores(self):
        self.assertTrue(is_scoring_position(HOME_HOOP[0], HOME_HOOP[1],
                                            (RIM_Z_LOW + RIM_Z_HIGH) / 2, HOME_HOOP))

    def test_too_low_misses(self):
        self.assertFalse(is_scoring_position(HOME_HOOP[0], HOME_HOOP[1], 2.0, HOME_HOOP))

    def test_off_target_misses(self):
        self.assertFalse(is_scoring_position(HOME_HOOP[0] - 10, HOME_HOOP[1], 14.5, HOME_HOOP))

    def test_wrong_hoop_misses(self):
        # A ball at the home rim is not a score for the away hoop.
        self.assertFalse(is_scoring_position(HOME_HOOP[0], HOME_HOOP[1], 14.5, AWAY_HOOP))


class TestBoundsAndClock(unittest.TestCase):
    def test_in_bounds(self):
        self.assertTrue(in_bounds(COURT_LENGTH / 2, COURT_WIDTH / 2))
        self.assertFalse(in_bounds(-1, 10))
        self.assertFalse(in_bounds(10, COURT_WIDTH + 5))

    def test_format_clock(self):
        self.assertEqual(format_clock(125), "2:05")
        self.assertEqual(format_clock(0), "0:00")
        self.assertEqual(format_clock(-5), "0:00")  # never negative


class TestShotModel(unittest.TestCase):
    def test_closer_is_more_likely(self):
        self.assertGreater(make_probability(8, 4), make_probability(8, 25))

    def test_skill_helps(self):
        self.assertGreater(make_probability(9, 15), make_probability(3, 15))

    def test_contest_hurts(self):
        self.assertLess(make_probability(7, 20, True), make_probability(7, 20, False))

    def test_always_clamped(self):
        self.assertGreaterEqual(make_probability(1, 40, True), 0.05)
        self.assertLessEqual(make_probability(10, 1), 0.95)


class TestFormation(unittest.TestCase):
    def test_five_distinct_in_bounds_spots(self):
        for team in ("home", "away"):
            spots = [offensive_spot(team, r) for r in ("PG", "SG", "SF", "PF", "C")]
            self.assertEqual(len(set(spots)), 5)
            for x, y in spots:
                self.assertTrue(in_bounds(x, y))

    def test_home_and_away_space_toward_own_hoop(self):
        # Home spaces toward the right (high x); away toward the left (low x).
        home_pg = offensive_spot("home", "PG")
        away_pg = offensive_spot("away", "PG")
        self.assertGreater(home_pg[0], COURT_LENGTH / 2)
        self.assertLess(away_pg[0], COURT_LENGTH / 2)


if __name__ == "__main__":
    unittest.main(verbosity=2)
