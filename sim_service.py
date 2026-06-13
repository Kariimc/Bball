# ==============================================================================
#                               SIM_SERVICE.PY
# ==============================================================================
# Thin transport layer that exposes the NBA simulation engine to a Godot (or any)
# front end. Pure stdlib -- no Flask / FastAPI -- so it runs anywhere Python does.
#
# The engine is deterministic, so the intended pattern is "fetch once, replay":
# the client requests a full game, then animates the returned play-by-play array
# at its own pace, using each possession's `clock` / `time_elapsed` to drive the
# on-screen game clock. No per-frame round trips needed.
#
# Two modes:
#   * HTTP server (interactive)   :  python sim_service.py --port 8765
#   * One-shot subprocess (stdin) :  python sim_service.py --once --seed 7 ...
#
# HTTP endpoints (all GET, all return JSON):
#   /health                                  -> {"status": "ok"}
#   /teams                                   -> available team keys + names
#   /simulate?seed=&difficulty=&home=&away=  -> full game (summary + play-by-play)
# ==============================================================================

from __future__ import annotations
import argparse
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any, Dict, Optional
from urllib.parse import urlparse, parse_qs

from nba_comprehensive_game_engine import NBAUnifiedEngine, build_team
from rosters_2005 import ROSTERS_2005, TEAM_NAMES_2005

DEFAULT_HOME = "SAS"
DEFAULT_AWAY = "DET"


# ------------------------------------------------------------------ core logic


def available_teams() -> Dict[str, str]:
    """Map of team key -> full display name."""
    return dict(TEAM_NAMES_2005)


def run_simulation(seed: Optional[int] = None, difficulty: float = 0.6,
                   home_key: str = DEFAULT_HOME, away_key: str = DEFAULT_AWAY,
                   include_play_by_play: bool = True) -> Dict[str, Any]:
    """Simulate one full game and return a JSON-serializable payload.

    This is the single source of truth for both the HTTP and subprocess modes.
    """
    if home_key not in ROSTERS_2005:
        raise KeyError(f"Unknown home team '{home_key}'. Choose from {list(ROSTERS_2005)}.")
    if away_key not in ROSTERS_2005:
        raise KeyError(f"Unknown away team '{away_key}'. Choose from {list(ROSTERS_2005)}.")

    home = build_team(TEAM_NAMES_2005[home_key], "HOME", ROSTERS_2005[home_key])
    away = build_team(TEAM_NAMES_2005[away_key], "AWAY", ROSTERS_2005[away_key])

    engine = NBAUnifiedEngine(home=home, away=away, seed=seed, difficulty=difficulty)
    summary = engine.simulate_game(verbose=False)

    payload: Dict[str, Any] = {
        "seed": seed,
        "difficulty": difficulty,
        "home_key": home_key,
        "away_key": away_key,
        "summary": summary,
    }
    if include_play_by_play:
        payload["play_by_play"] = engine.play_by_play
    return payload


# ----------------------------------------------------------------- HTTP server


class _SimHandler(BaseHTTPRequestHandler):
    server_version = "BballSim/1.0"

    def _send_json(self, payload: Dict[str, Any], status: int = 200) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        # Permissive CORS so an exported HTML5/Godot-web build can call locally.
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

    @staticmethod
    def _qs_int(qs: Dict[str, list], key: str) -> Optional[int]:
        if key in qs and qs[key] and qs[key][0] != "":
            return int(qs[key][0])
        return None

    @staticmethod
    def _qs_float(qs: Dict[str, list], key: str, default: float) -> float:
        if key in qs and qs[key]:
            return float(qs[key][0])
        return default

    @staticmethod
    def _qs_str(qs: Dict[str, list], key: str, default: str) -> str:
        if key in qs and qs[key]:
            return qs[key][0].upper()
        return default

    def do_GET(self) -> None:  # noqa: N802 (stdlib naming)
        parsed = urlparse(self.path)
        route = parsed.path.rstrip("/") or "/"
        qs = parse_qs(parsed.query)
        try:
            if route in ("/", "/health"):
                self._send_json({"status": "ok", "service": self.server_version})
            elif route == "/teams":
                self._send_json({"teams": available_teams()})
            elif route == "/simulate":
                payload = run_simulation(
                    seed=self._qs_int(qs, "seed"),
                    difficulty=self._qs_float(qs, "difficulty", 0.6),
                    home_key=self._qs_str(qs, "home", DEFAULT_HOME),
                    away_key=self._qs_str(qs, "away", DEFAULT_AWAY),
                    include_play_by_play=self._qs_str(qs, "pbp", "1") not in ("0", "FALSE", "NO"),
                )
                self._send_json(payload)
            else:
                self._send_json({"error": f"unknown route '{route}'"}, status=404)
        except (KeyError, ValueError) as exc:
            self._send_json({"error": str(exc)}, status=400)
        except Exception as exc:  # pragma: no cover - defensive
            self._send_json({"error": f"internal: {exc}"}, status=500)

    def log_message(self, fmt: str, *args: Any) -> None:
        # Quieter default logging; comment out to see every request.
        pass


def serve(port: int = 8765, host: str = "127.0.0.1") -> None:
    httpd = ThreadingHTTPServer((host, port), _SimHandler)
    print(f"[sim_service] serving on http://{host}:{port}")
    print(f"[sim_service]   GET /health")
    print(f"[sim_service]   GET /teams")
    print(f"[sim_service]   GET /simulate?seed=2005&difficulty=0.7&home=SAS&away=DET")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n[sim_service] shutting down")
        httpd.shutdown()


# ------------------------------------------------------------------------ CLI


def main() -> None:
    parser = argparse.ArgumentParser(description="Bball simulation service")
    parser.add_argument("--port", type=int, default=8765, help="HTTP server port")
    parser.add_argument("--host", default="127.0.0.1", help="HTTP bind host")
    parser.add_argument("--once", action="store_true",
                        help="Print one simulation as JSON to stdout and exit (subprocess mode)")
    parser.add_argument("--seed", type=int, default=None)
    parser.add_argument("--difficulty", type=float, default=0.6)
    parser.add_argument("--home", default=DEFAULT_HOME)
    parser.add_argument("--away", default=DEFAULT_AWAY)
    parser.add_argument("--no-pbp", action="store_true", help="Omit play-by-play (summary only)")
    args = parser.parse_args()

    if args.once:
        payload = run_simulation(
            seed=args.seed, difficulty=args.difficulty,
            home_key=args.home.upper(), away_key=args.away.upper(),
            include_play_by_play=not args.no_pbp,
        )
        print(json.dumps(payload))
    else:
        serve(port=args.port, host=args.host)


if __name__ == "__main__":
    main()
