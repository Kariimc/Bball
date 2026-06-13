# ==============================================================================
#                          TESTS / TEST_IMPORT.PY
# ==============================================================================
# Regression suite for URL/file team import: validation, normalization, the SSRF
# guard, registration, and an end-to-end HTTP fetch against a local test server.
# ==============================================================================

from __future__ import annotations
import json
import os
import sys
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from team_import import (  # noqa: E402
    normalize_player, validate_roster, load_team_data_from_file,
    load_team_data_from_url, register_imported_team, _assert_public_url,
)
from rosters import get_roster, team_name  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
TEMPLATE = os.path.join(os.path.dirname(HERE), "examples", "team_template.json")

with open(TEMPLATE, "r", encoding="utf-8") as _fh:
    _TEMPLATE_BYTES = _fh.read().encode("utf-8")


class _Handler(BaseHTTPRequestHandler):
    def do_GET(self):  # noqa: N802
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(_TEMPLATE_BYTES)

    def log_message(self, *a):
        pass


# ------------------------------------------------------------- normalization


class TestNormalization(unittest.TestCase):
    def test_clamps_out_of_range_stats(self):
        p = normalize_player({"name": "X", "role": "PG",
                              "stats": {"three_point": 999, "speed": -5, "vertical_leap": 99}})
        self.assertEqual(p["stats"]["three_point"], 100)
        self.assertEqual(p["stats"]["speed"], 0)
        self.assertEqual(p["stats"]["vertical_leap"], 50)  # special range cap

    def test_unknown_stat_keys_dropped(self):
        p = normalize_player({"name": "X", "stats": {"hacking_skill": 99, "three_point": 70}})
        self.assertNotIn("hacking_skill", p["stats"])
        self.assertIn("three_point", p["stats"])

    def test_invalid_role_defaults_to_sf(self):
        self.assertEqual(normalize_player({"name": "X", "role": "QB"})["role"], "SF")

    def test_height_clamped_and_defaulted(self):
        self.assertEqual(normalize_player({"name": "X", "height_inches": 200})["height_inches"], 96.0)
        self.assertEqual(normalize_player({"name": "X"})["height_inches"], 78.0)

    def test_missing_name_raises(self):
        with self.assertRaises(ValueError):
            normalize_player({"role": "PG"})


# ------------------------------------------------------------- roster validation


class TestValidation(unittest.TestCase):
    def _players(self, n):
        return [{"name": f"P{i}", "role": "PG"} for i in range(n)]

    def test_accepts_team_object(self):
        out = validate_roster({"name": "T", "key": "tk", "players": self._players(6)})
        self.assertEqual(out["name"], "T")
        self.assertEqual(out["key"], "TK")
        self.assertEqual(len(out["players"]), 6)

    def test_accepts_bare_list(self):
        out = validate_roster(self._players(5), name="Listers")
        self.assertEqual(out["name"], "Listers")

    def test_rejects_too_few_players(self):
        with self.assertRaises(ValueError):
            validate_roster(self._players(4))

    def test_rejects_too_many_players(self):
        with self.assertRaises(ValueError):
            validate_roster(self._players(21))

    def test_warns_on_missing_positions(self):
        out = validate_roster(self._players(5))  # all PG
        self.assertTrue(out["warnings"])


# ------------------------------------------------------------- SSRF guard


class TestSsrfGuard(unittest.TestCase):
    def test_rejects_non_http_scheme(self):
        with self.assertRaises(ValueError):
            _assert_public_url("file:///etc/passwd", allow_private=False)

    def test_rejects_loopback(self):
        with self.assertRaises(ValueError):
            _assert_public_url("http://127.0.0.1/x.json", allow_private=False)

    def test_rejects_private_range(self):
        with self.assertRaises(ValueError):
            _assert_public_url("http://10.0.0.5/x.json", allow_private=False)

    def test_allow_private_override(self):
        # Should not raise when explicitly permitted.
        self.assertTrue(_assert_public_url("http://127.0.0.1/x.json", allow_private=True))


# ------------------------------------------------------------- file + URL load


class TestLoaders(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = ThreadingHTTPServer(("127.0.0.1", 0), _Handler)
        cls.port = cls.server.server_address[1]
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()

    def test_load_from_file(self):
        out = load_team_data_from_file(TEMPLATE)
        self.assertEqual(out["name"], "Custom City Comets")
        self.assertEqual(len(out["players"]), 8)

    def test_load_from_url_local(self):
        url = f"http://127.0.0.1:{self.port}/comets.json"
        out = load_team_data_from_url(url, allow_private=True)
        self.assertEqual(out["name"], "Custom City Comets")

    def test_url_loopback_blocked_without_override(self):
        url = f"http://127.0.0.1:{self.port}/comets.json"
        with self.assertRaises(ValueError):
            load_team_data_from_url(url)

    def test_max_bytes_enforced(self):
        url = f"http://127.0.0.1:{self.port}/comets.json"
        with self.assertRaises(ValueError):
            load_team_data_from_url(url, allow_private=True, max_bytes=10)

    def test_register_and_use_imported_team(self):
        out = load_team_data_from_file(TEMPLATE, key="TEST1")
        key = register_imported_team(out)
        self.assertEqual(key, "TEST1")
        self.assertEqual(team_name("TEST1"), "Custom City Comets")
        self.assertEqual(len(get_roster("TEST1")), 8)


if __name__ == "__main__":
    unittest.main(verbosity=2)
