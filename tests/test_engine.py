# ==============================================================================
#                            TESTS / TEST_ENGINE.PY
# ==============================================================================
# Functional regression suite for the NBA simulation engine. Pure stdlib
# (unittest) -- no external dependencies, deterministic via fixed seeds.
#
# Run:  python -m unittest discover -s tests -v
#   or: python -m pytest tests/ -q   (if pytest is installed)
# ==============================================================================

from __future__ import annotations
import math
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from nba_comprehensive_game_engine import (  # noqa: E402
    Vector3, EntityProxy, Team, GameState, GameScenarioEngine, NBAPlayCaller,
    NBAUnifiedEngine, JumpBallEngine, LooseBallScrambleSystem, LiveBallObject,
    FastBreakEngine, build_team, SHOT_CURVE, BallState, BONUS_FOUL_THRESHOLD,
    OffensiveStrategy, DefensivePosture, DefensiveScheme, OffensivePlay,
)
from rosters_2005 import SPURS_2005, PISTONS_2005  # noqa: E402

import random


# ------------------------------------------------------------------ Vector math


class TestVector3(unittest.TestCase):
    def test_distance(self):
        self.assertAlmostEqual(Vector3(0, 0, 0).distance_to(Vector3(3, 0, 4)), 5.0)

    def test_planar_distance_ignores_y(self):
        self.assertAlmostEqual(Vector3(0, 99, 0).planar_distance_to(Vector3(3, 0, 4)), 5.0)

    def test_normalized_unit_length(self):
        self.assertAlmostEqual(Vector3(0, 3, 4).normalized().magnitude(), 1.0)

    def test_normalized_zero_is_safe(self):
        self.assertEqual(Vector3().normalized(), Vector3())

    def test_dot_and_ops(self):
        self.assertAlmostEqual(Vector3(1, 2, 3).dot(Vector3(4, 5, 6)), 32.0)
        self.assertEqual(Vector3(1, 1, 1) + Vector3(2, 3, 4), Vector3(3, 4, 5))
        self.assertEqual((Vector3(2, 4, 6) * 0.5), Vector3(1, 2, 3))

    def test_point_to_segment_perpendicular(self):
        # Point directly above the middle of a segment on the x-axis.
        d = Vector3.distance_point_to_segment(Vector3(5, 0, 3), Vector3(0, 0, 0), Vector3(10, 0, 0))
        self.assertAlmostEqual(d, 3.0)

    def test_point_to_segment_clamps_to_endpoint(self):
        # Point beyond the segment end clamps to the endpoint distance.
        d = Vector3.distance_point_to_segment(Vector3(20, 0, 0), Vector3(0, 0, 0), Vector3(10, 0, 0))
        self.assertAlmostEqual(d, 10.0)


# ----------------------------------------------------------------- Entity/Team


class TestEntityAndTeam(unittest.TestCase):
    def test_missing_stats_fall_back_to_defaults(self):
        p = EntityProxy("X", "HOME", "Nobody", stats={"three_point": 99})
        self.assertEqual(p.rating("three_point"), 99)
        self.assertGreater(p.rating("speed"), 0)  # filled from DEFAULT_STATS

    def test_fatigue_erodes_effective_rating(self):
        p = EntityProxy("X", "HOME", "Tired", stats={"speed": 100})
        self.assertEqual(p.effective("speed"), 100)
        p.fatigue = 1.0
        self.assertAlmostEqual(p.effective("speed"), 85.0)

    def test_build_team_assigns_unique_ids_and_five_starters(self):
        team = build_team("SAS", "HOME", SPURS_2005)
        ids = [p.id for p in team.roster]
        self.assertEqual(len(ids), len(set(ids)))
        self.assertEqual(len(team.on_court), 5)

    def test_starters_cover_all_positions(self):
        team = build_team("DET", "AWAY", PISTONS_2005)
        roles = {p.role for p in team.on_court}
        self.assertEqual(roles, {"PG", "SG", "SF", "PF", "C"})

    def test_bench_is_disjoint_from_on_court(self):
        team = build_team("SAS", "HOME", SPURS_2005)
        self.assertTrue(set(p.id for p in team.bench).isdisjoint(p.id for p in team.on_court))


# ----------------------------------------------------- GameState single source


class TestGameStateAndScenario(unittest.TestCase):
    def test_scenario_reads_gamestate_not_duplicate(self):
        gs = GameState()
        scn = GameScenarioEngine(gs)
        gs.score["HOME"] = 50
        self.assertEqual(scn.home_score, 50)
        scn.away_score = 60  # write-through shim
        self.assertEqual(gs.score["AWAY"], 60)

    def test_bonus_threshold(self):
        gs = GameState()
        self.assertFalse(gs.in_bonus("HOME"))
        gs.team_fouls["HOME"] = BONUS_FOUL_THRESHOLD
        self.assertTrue(gs.in_bonus("HOME"))

    def test_desperation_zero_before_q4(self):
        gs = GameState(); gs.quarter = 3; gs.score = {"HOME": 80, "AWAY": 90}
        scn = GameScenarioEngine(gs)
        self.assertEqual(scn.calculate_desperation_index("HOME"), 0.0)

    def test_desperation_positive_when_trailing_late(self):
        gs = GameState(); gs.quarter = 4; gs.game_clock = 60.0
        gs.score = {"HOME": 95, "AWAY": 103}
        scn = GameScenarioEngine(gs)
        self.assertGreater(scn.calculate_desperation_index("HOME"), 0.0)
        self.assertEqual(scn.calculate_desperation_index("AWAY"), 0.0)  # leader not desperate

    def test_trailing_team_hunts_threes_when_down_big_late(self):
        gs = GameState(); gs.quarter = 4; gs.game_clock = 90.0
        gs.score = {"HOME": 90, "AWAY": 100}  # HOME down 10
        scn = GameScenarioEngine(gs)
        off, _ = scn.derive_tactical_overrides("HOME")
        self.assertEqual(off, OffensiveStrategy.HUNT_THREE_POINTERS)

    def test_leading_team_reduces_clock_late(self):
        gs = GameState(); gs.quarter = 4; gs.game_clock = 100.0
        gs.score = {"HOME": 105, "AWAY": 100}  # HOME leads
        scn = GameScenarioEngine(gs)
        off, _ = scn.derive_tactical_overrides("HOME")
        self.assertEqual(off, OffensiveStrategy.REDUCE_CLOCK_ISO)

    def test_intentional_foul_when_down_small_under_minute(self):
        gs = GameState(); gs.quarter = 4; gs.game_clock = 45.0
        gs.score = {"HOME": 100, "AWAY": 105}  # HOME down 5
        scn = GameScenarioEngine(gs)
        _, posture = scn.derive_tactical_overrides("HOME")
        self.assertEqual(posture, DefensivePosture.INTENTIONAL_FOUL_STANCE)


# -------------------------------------------------------------- Playcaller math


class TestPlayCaller(unittest.TestCase):
    def test_drop_coverage_punishes_paint_play(self):
        pc = NBAPlayCaller()
        pc.coach_call_offensive_play(OffensivePlay.HORNS_PICK_AND_ROLL)  # PAINT
        self.assertLess(pc.evaluate_strategic_matchup(DefensiveScheme.DROP_COVERAGE_PAINT), 0.0)

    def test_man_to_man_returns_play_bonus(self):
        pc = NBAPlayCaller()
        pc.coach_call_offensive_play(OffensivePlay.LOOP_ZIPPER_THREE)
        val = pc.evaluate_strategic_matchup(DefensiveScheme.MAN_TO_MAN_STICKY)
        self.assertAlmostEqual(val, pc.offensive_playbook[OffensivePlay.LOOP_ZIPPER_THREE].shot_quality_bonus)

    def test_default_arg_uses_own_defense(self):
        pc = NBAPlayCaller()
        pc.coach_adapt_defensive_scheme(DefensiveScheme.DROP_COVERAGE_PAINT)
        pc.coach_call_offensive_play(OffensivePlay.MOTION_FLOW_4OUT)  # PAINT
        self.assertLess(pc.evaluate_strategic_matchup(), 0.0)


# ------------------------------------------------------------- Subsystem checks


class TestSubsystems(unittest.TestCase):
    def test_jump_ball_returns_winner_with_ball(self):
        a = EntityProxy("A", "HOME", "Big A", stats={"vertical_leap": 40})
        b = EntityProxy("B", "AWAY", "Big B", stats={"vertical_leap": 20})
        jb = JumpBallEngine(a, b, random.Random(1)); jb.initiate_toss()
        winner, text = jb.execute_tip()
        self.assertIn(winner, (a, b))
        self.assertTrue(winner.has_ball)
        self.assertIsInstance(text, str)

    def test_scramble_recovers_loose_ball(self):
        rng = random.Random(3)
        scr = LooseBallScrambleSystem(rng)
        ball = LiveBallObject(); ball.state = BallState.LOOSE_ON_FLOOR
        ball.position = Vector3(25, 0, 47)
        p = EntityProxy("P", "HOME", "Hustler", stats={"hustle": 100})
        p.position = Vector3(25, 0, 47)
        result = scr.process_hustle_scramble_tick(ball, [p])
        self.assertIn(result["event"], ("LOOSE_BALL_RECOVERY", "SCRUM_WON",
                                        "HELD_BALL_WHISTLE", "HEROIC_SAVE"))

    def test_scramble_noop_when_not_loose(self):
        scr = LooseBallScrambleSystem(random.Random(0))
        ball = LiveBallObject(); ball.state = BallState.POSSESSION_HELD
        self.assertEqual(scr.process_hustle_scramble_tick(ball, [])["event"], "NO_ACTION")

    def test_fast_break_runaway_with_no_defenders(self):
        fb = FastBreakEngine(random.Random(0))
        passer = EntityProxy("A", "HOME", "P"); attacker = EntityProxy("B", "HOME", "A")
        broke, _ = fb.evaluate_live_turnover_transition(passer, attacker, [])
        self.assertTrue(broke)

    def test_shot_curve_bounds_are_sane(self):
        for key, (base, span) in SHOT_CURVE.items():
            self.assertGreaterEqual(base, 0.0)
            self.assertLessEqual(base + span, 1.0, f"{key} can exceed 100%")


# ------------------------------------------------------------ Full-game checks


class TestFullGame(unittest.TestCase):
    def _play(self, seed=2005):
        engine = NBAUnifiedEngine(
            home=build_team("San Antonio Spurs", "HOME", SPURS_2005),
            away=build_team("Detroit Pistons", "AWAY", PISTONS_2005),
            seed=seed, difficulty=0.6)
        return engine, engine.simulate_game()

    def test_determinism_same_seed_same_result(self):
        _, g1 = self._play(7)
        _, g2 = self._play(7)
        self.assertEqual(g1["final_score"], g2["final_score"])
        self.assertEqual(g1["home"]["totals"], g2["home"]["totals"])

    def test_different_seeds_differ(self):
        _, g1 = self._play(1)
        _, g2 = self._play(2)
        self.assertNotEqual((g1["final_score"], g1["possessions"]),
                            (g2["final_score"], g2["possessions"]))

    def test_no_tie_at_final(self):
        _, g = self._play(11)
        self.assertNotEqual(g["final_score"]["HOME"], g["final_score"]["AWAY"])

    def test_score_consistency_players_sum_to_team(self):
        engine, g = self._play(5)
        for side, block in (("HOME", g["home"]), ("AWAY", g["away"])):
            player_pts = sum(p["pts"] for p in block["players"])
            self.assertEqual(player_pts, block["score"])
            self.assertEqual(block["score"], engine.game_state.score[side])

    def test_points_reconcile_with_fg_and_ft(self):
        _, g = self._play(9)
        for block in (g["home"], g["away"]):
            t = block["totals"]
            expected = 2 * (t["fgm"] - t["tpm"]) + 3 * t["tpm"] + t["ftm"]
            self.assertEqual(expected, t["pts"], f"{block['name']} points don't reconcile")

    def test_box_score_internal_invariants(self):
        _, g = self._play(13)
        for block in (g["home"], g["away"]):
            for p in block["players"]:
                self.assertLessEqual(p["fgm"], p["fga"])
                self.assertLessEqual(p["tpm"], p["tpa"])
                self.assertLessEqual(p["ftm"], p["fta"])
                self.assertLessEqual(p["tpm"], p["fgm"])  # threes are made FGs
                self.assertLessEqual(p["tpa"], p["fga"])
                for k in ("pts", "fga", "fta", "reb", "ast", "stl", "blk", "tov", "pf"):
                    self.assertGreaterEqual(p[k], 0, f"{p['name']} negative {k}")

    def test_realistic_score_band(self):
        # Two elite defensive teams: expect a believable total, never absurd.
        _, g = self._play(3)
        total = g["final_score"]["HOME"] + g["final_score"]["AWAY"]
        self.assertGreater(total, 120)
        self.assertLess(total, 280)

    def test_possession_count_realistic(self):
        _, g = self._play(4)
        # ~200 possessions for regulation; OT adds more.
        self.assertGreater(g["possessions"], 140)
        self.assertLess(g["possessions"], 320)

    def test_telemetry_is_serializable(self):
        import json
        _, g = self._play(6)
        json.dumps(g)  # raises if any non-serializable object leaked in


if __name__ == "__main__":
    unittest.main(verbosity=2)
