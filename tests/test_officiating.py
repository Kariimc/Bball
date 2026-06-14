# ==============================================================================
#                         TESTS / TEST_OFFICIATING.PY
# ==============================================================================
# Referee AI, real-time rulebook, jump-ball kinematics, free-throw sequence.
# ==============================================================================

from __future__ import annotations
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from nba_comprehensive_game_engine import Vector3, EntityProxy, LiveBallObject  # noqa: E402
from officiating import (  # noqa: E402
    RefereeAI, RefState, LiveOfficiatingEngine, JumpBallSimulation, FreeThrowSystem,
)


class TestRefereeAI(unittest.TestCase):
    def test_retrieval_then_handoff(self):
        ball = LiveBallObject()
        ball.position = Vector3(25, 0, 47)
        player = EntityProxy("P", "HOME", "Guard")
        player.position = Vector3(25, 0, 40)
        ref = RefereeAI(ball, start=Vector3(25, 0, 47))
        ref.trigger_retrieval(hand_to=player)
        for _ in range(2000):                      # run until handoff completes
            ref.update(0.1)
            if ref.state == RefState.IDLE and not ref.is_holding_ball:
                break
        self.assertEqual(ref.state, RefState.IDLE)
        # Ball ends up at the player.
        self.assertLess(ball.position.distance_to(player.position), 2.0)


class TestLiveOfficiating(unittest.TestCase):
    def _live_engine(self):
        eng = LiveOfficiatingEngine()
        eng.is_dead_ball = False
        return eng

    def test_shot_clock_violation(self):
        eng = self._live_engine()
        out = None
        for _ in range(260):                        # 26s of 0.1 ticks
            out = eng.update_clocks(0.1)
            if out:
                break
        self.assertEqual(out["type"], "24-SECOND SHOT CLOCK")

    def test_backcourt_violation(self):
        eng = self._live_engine()
        out = None
        for _ in range(100):
            out = eng.update_clocks(0.1, ball_carrier=True, in_frontcourt=False)
            if out:
                break
        self.assertEqual(out["type"], "8-SECOND BACKCOURT")

    def test_closely_guarded_violation(self):
        eng = self._live_engine()
        out = None
        for _ in range(60):
            out = eng.update_clocks(0.1, ball_carrier=True, in_frontcourt=True, defense_nearby=True)
            if out:
                break
        self.assertEqual(out["type"], "5-SECOND CLOSELY GUARDED")

    def test_traveling(self):
        eng = self._live_engine()
        self.assertIsNone(eng.record_travel("p", moving=True, gathered=True, dribbling=False))
        self.assertIsNone(eng.record_travel("p", moving=True, gathered=True, dribbling=False))
        out = eng.record_travel("p", moving=True, gathered=True, dribbling=False)
        self.assertEqual(out["type"], "TRAVELING")

    def test_double_dribble_and_carry(self):
        eng = self._live_engine()
        self.assertEqual(eng.check_double_dribble(True, False, True)["type"], "DOUBLE DRIBBLE")
        self.assertIsNone(eng.check_double_dribble(False, False, True))
        self.assertEqual(eng.check_carry(True, True)["type"], "CARRYING / PALMING")

    def test_offensive_three_seconds(self):
        eng = self._live_engine()
        out = None
        for _ in range(40):
            out = eng.record_paint("c", is_offense=True, in_paint=True, guarding=False, dt=0.1)
            if out:
                break
        self.assertEqual(out["type"], "OFFENSIVE 3-SECONDS")

    def test_shooting_foul_three_shots_beyond_arc(self):
        eng = self._live_engine()
        rec = eng.commit_foul("AWAY", "SHOOTING FOUL", "HOME", shooting=True, beyond_arc=True)
        self.assertEqual(rec["penalty"], "3_FREE_THROWS")

    def test_charge_is_offensive_turnover(self):
        eng = self._live_engine()
        rec = eng.commit_foul("HOME", "CHARGE", "AWAY", charge=True)
        self.assertIn("TURNOVER", rec["penalty"])
        self.assertEqual(eng.team_fouls["HOME"], 0)   # charges don't add team fouls

    def test_bonus_after_threshold(self):
        eng = self._live_engine()
        last = None
        for _ in range(5):
            last = eng.commit_foul("AWAY", "LOOSE BALL FOUL", "HOME")
        self.assertEqual(last["penalty"], "PENALTY_BONUS_2_SHOTS")

    def test_period_expiration_resets(self):
        eng = self._live_engine()
        eng.game_clock = 0.05
        out = eng.update_clocks(0.1)
        self.assertEqual(out["event"], "QUARTER_EXPIRED")
        self.assertEqual(eng.quarter, 2)
        self.assertEqual(eng.team_fouls["HOME"], 0)


class TestJumpBall(unittest.TestCase):
    def test_apex_and_descent(self):
        ball = LiveBallObject()
        jb = JumpBallSimulation(ball)
        apex = jb.initiate_toss(7.5)
        self.assertGreater(apex, 0.0)
        # perfect tip timing scores 1.0, off-timing less
        self.assertAlmostEqual(jb.process_tip_input(apex), 1.0, places=5)
        self.assertLess(jb.process_tip_input(apex + 0.3), jb.process_tip_input(apex + 0.05))

    def test_toss_completes(self):
        ball = LiveBallObject()
        jb = JumpBallSimulation(ball)
        jb.initiate_toss(7.5)
        for _ in range(500):
            jb.update(0.02)
        self.assertFalse(jb.active)


class TestFreeThrows(unittest.TestCase):
    def test_sequence_completes(self):
        shooter = EntityProxy("S", "HOME", "Shooter", stats={"free_throw": 90})
        ft = FreeThrowSystem()
        ft.initiate(shooter, 2)
        r1 = ft.process_shot(0.0)
        self.assertEqual(r1["shot"], 1)
        self.assertNotIn("sequence_complete", r1)
        r2 = ft.process_shot(0.0)
        self.assertTrue(r2.get("sequence_complete"))
        self.assertFalse(ft.active)

    def test_shooter_warped_to_line(self):
        shooter = EntityProxy("S", "HOME", "Shooter")
        ft = FreeThrowSystem()
        ft.initiate(shooter, 1)
        self.assertAlmostEqual(shooter.position.z, 19.0)


if __name__ == "__main__":
    unittest.main(verbosity=2)
