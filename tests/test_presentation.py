# ==============================================================================
#                        TESTS / TEST_PRESENTATION.PY
# ==============================================================================
# Event bus + presentation layer (crowd, mascot/cheer, camera, bench, coaches).
# ==============================================================================

from __future__ import annotations
import json
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from game_events import EventBus, GameEvent  # noqa: E402
from presentation import (  # noqa: E402
    PresentationDirector, DynamicCrowdSystem, EntertainmentManager,
    CameraDirector, SidelineUnit, CrowdState, CameraMode, BenchState, CoachState,
)
from nba_comprehensive_game_engine import Vector3  # noqa: E402


class TestEventBus(unittest.TestCase):
    def test_subscribe_and_emit(self):
        bus = EventBus()
        seen = []
        bus.subscribe(GameEvent.SCORE, lambda p: seen.append(p["points"]))
        bus.emit(GameEvent.SCORE, points=3)
        self.assertEqual(seen, [3])
        self.assertEqual(bus.history[-1]["event"], "SCORE")

    def test_handler_error_is_isolated(self):
        bus = EventBus()
        hit = []
        bus.subscribe(GameEvent.SCORE, lambda p: (_ for _ in ()).throw(ValueError("boom")))
        bus.subscribe(GameEvent.SCORE, lambda p: hit.append(1))
        rec = bus.emit(GameEvent.SCORE, points=2)
        self.assertEqual(hit, [1])                  # second handler still ran
        self.assertIn("handler_errors", rec)


class TestCrowd(unittest.TestCase):
    def test_state_transitions(self):
        c = DynamicCrowdSystem()
        c.synchronize_state(50, 40, current_run=8)
        self.assertEqual(c.state, CrowdState.STANDING_SUPER_EXCITED)
        c.synchronize_state(40, 55, current_run=0)
        self.assertEqual(c.state, CrowdState.SITTING_DEJECTED)
        c.synchronize_state(50, 49, current_run=0)
        self.assertEqual(c.state, CrowdState.SITTING_IDLE)

    def test_momentum_moves_with_run(self):
        c = DynamicCrowdSystem()
        base = c.home_momentum
        c.synchronize_state(60, 40, current_run=10)
        self.assertGreater(c.home_momentum, base)


class TestEntertainment(unittest.TestCase):
    def test_halftime_sets_routine(self):
        e = EntertainmentManager(DynamicCrowdSystem())
        e.trigger_halftime_show()
        self.assertTrue(e.halftime_active)
        self.assertEqual(e.mascot.current_anim, "Mascot_Halftime_Routine")

    def test_cannon_returns_launch_vector(self):
        e = EntertainmentManager(DynamicCrowdSystem())
        origin, vel = e.fire_mascot_tshirt_cannon(Vector3(40, 20, 30))
        self.assertIsInstance(origin, Vector3)
        self.assertGreater(vel.magnitude(), 0.0)


class TestCamera(unittest.TestCase):
    def test_celebration_holds_then_resumes(self):
        cam = CameraDirector()
        cam.cut_to_celebration(Vector3(1, 0, 1), hold=2.0)
        self.assertEqual(cam.mode, CameraMode.TRACK_CELEBRATION)
        cam.follow_ball(Vector3(5, 0, 5))          # ignored during hold
        self.assertEqual(cam.mode, CameraMode.TRACK_CELEBRATION)
        cam.update(2.5)                            # hold elapses
        cam.follow_ball(Vector3(5, 0, 5))
        self.assertEqual(cam.mode, CameraMode.TRACK_BALL)


class TestSideline(unittest.TestCase):
    def test_state_changes_drive_animation(self):
        s = SidelineUnit("HOME")
        s.set_coach(CoachState.ARGUING_CALL)
        s.set_bench(BenchState.BENCH_MOB_CELEBRATION)
        self.assertEqual(s.coach_state, CoachState.ARGUING_CALL)
        self.assertEqual(s.bench.current_anim, "Bench_Mob_Celebration")


class TestDirector(unittest.TestCase):
    def _director(self):
        bus = EventBus()
        return bus, PresentationDirector(bus)

    def test_score_reacts(self):
        bus, d = self._director()
        bus.emit(GameEvent.SCORE, team="HOME", points=3, at=[25, 0, 5])
        self.assertEqual(d.camera.mode, CameraMode.TRACK_CELEBRATION)
        self.assertEqual(d.home_sideline.bench_state, BenchState.BENCH_MOB_CELEBRATION)

    def test_halftime_on_q2_end(self):
        bus, d = self._director()
        bus.emit(GameEvent.PERIOD_END, quarter=2)
        self.assertTrue(d.entertainment.halftime_active)

    def test_timeout_huddles_both_benches(self):
        bus, d = self._director()
        bus.emit(GameEvent.TIMEOUT)
        self.assertEqual(d.home_sideline.coach_state, CoachState.TIMEOUT_HUDDLE)
        self.assertEqual(d.away_sideline.coach_state, CoachState.TIMEOUT_HUDDLE)

    def test_telemetry_serializable(self):
        bus, d = self._director()
        bus.emit(GameEvent.SCORE, team="AWAY", points=2)
        json.dumps(d.to_telemetry())


if __name__ == "__main__":
    unittest.main(verbosity=2)
