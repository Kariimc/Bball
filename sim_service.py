# ==============================================================================
#                               SIM_SERVICE.PY
# ==============================================================================
# Transport layer exposing the simulation engine to a Godot (or any) front end.
# Pure stdlib -- no Flask / FastAPI -- so it runs anywhere Python does.
#
# TWO RENDERING MODES
#   1. Replay (deterministic, simplest):
#        GET /simulate -> a whole game; client animates the play-by-play array.
#   2. Live step-through (stateful session):
#        GET /start      -> create a game session, get id + starting lineups
#        GET /possession -> advance ONE possession; render it; repeat
#      Use this when you want Godot to drive the pace tick-by-tick.
#
# PLAYOFFS
#        GET /series   -> best-of-N series (injuries persist across games)
#        GET /bracket  -> single-elimination bracket
#
# Run:
#   python sim_service.py --port 8765            # HTTP server
#   python sim_service.py --once --seed 7 ...    # one game as JSON to stdout
# ==============================================================================

from __future__ import annotations
import argparse
import json
import threading
import uuid
from collections import OrderedDict
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any, Dict, Optional
from urllib.parse import urlparse, parse_qs

from nba_comprehensive_game_engine import NBAUnifiedEngine, build_team
from rosters import all_teams, get_roster, team_name
from series import simulate_series, simulate_bracket

DEFAULT_HOME = "SAS"
DEFAULT_AWAY = "DET"
MAX_SESSIONS = 100

# session_id -> engine, with a lock for the ThreadingHTTPServer.
_SESSIONS: "OrderedDict[str, NBAUnifiedEngine]" = OrderedDict()
_LOCK = threading.Lock()


# ------------------------------------------------------------------ core logic


def _build_engine(seed: Optional[int], difficulty: float, home_key: str,
                  away_key: str, injury_rate: float) -> NBAUnifiedEngine:
    home = build_team(team_name(home_key), "HOME", get_roster(home_key))
    away = build_team(team_name(away_key), "AWAY", get_roster(away_key))
    return NBAUnifiedEngine(home=home, away=away, seed=seed,
                            difficulty=difficulty, injury_rate=injury_rate)


def _lineup(team) -> list:
    return [{"id": p.id, "name": p.name, "role": p.role,
             "height_inches": p.height_inches} for p in team.on_court]


def run_simulation(seed: Optional[int] = None, difficulty: float = 0.6,
                   home_key: str = DEFAULT_HOME, away_key: str = DEFAULT_AWAY,
                   injury_rate: float = 0.0005,
                   include_play_by_play: bool = True) -> Dict[str, Any]:
    engine = _build_engine(seed, difficulty, home_key.upper(), away_key.upper(), injury_rate)
    summary = engine.simulate_game(verbose=False)
    payload: Dict[str, Any] = {
        "seed": seed, "difficulty": difficulty,
        "home_key": home_key.upper(), "away_key": away_key.upper(),
        "summary": summary,
    }
    if include_play_by_play:
        payload["play_by_play"] = engine.play_by_play
    return payload


def start_session(seed: Optional[int], difficulty: float, home_key: str,
                  away_key: str, injury_rate: float) -> Dict[str, Any]:
    engine = _build_engine(seed, difficulty, home_key.upper(), away_key.upper(), injury_rate)
    opening = engine.start_game()
    sid = uuid.uuid4().hex
    with _LOCK:
        _SESSIONS[sid] = engine
        while len(_SESSIONS) > MAX_SESSIONS:
            _SESSIONS.popitem(last=False)  # evict oldest
    return {
        "session": sid,
        "home": {"key": home_key.upper(), "name": engine.home.name, "lineup": _lineup(engine.home)},
        "away": {"key": away_key.upper(), "name": engine.away.name, "lineup": _lineup(engine.away)},
        "opening": opening,
    }


def step_session(session_id: str) -> Dict[str, Any]:
    with _LOCK:
        engine = _SESSIONS.get(session_id)
    if engine is None:
        raise KeyError(f"unknown session '{session_id}'")
    result = engine.step_possession()
    if result.get("game_over"):
        with _LOCK:
            _SESSIONS.pop(session_id, None)  # auto-cleanup finished games
    return result


def session_state(session_id: str) -> Dict[str, Any]:
    with _LOCK:
        engine = _SESSIONS.get(session_id)
    if engine is None:
        raise KeyError(f"unknown session '{session_id}'")
    gs = engine.game_state
    return {"session": session_id, "quarter": gs.quarter,
            "clock": gs.game_clock, "score": dict(gs.score),
            "possession": gs.possession_side, "game_over": engine._game_over}


# ----------------------------------------------------------------- HTTP server


class _SimHandler(BaseHTTPRequestHandler):
    server_version = "BballSim/2.0"

    def _send_json(self, payload: Dict[str, Any], status: int = 200) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

    @staticmethod
    def _i(qs, key) -> Optional[int]:
        if key in qs and qs[key] and qs[key][0] != "":
            return int(qs[key][0])
        return None

    @staticmethod
    def _f(qs, key, default) -> float:
        return float(qs[key][0]) if key in qs and qs[key] else default

    @staticmethod
    def _s(qs, key, default) -> str:
        return qs[key][0] if key in qs and qs[key] else default

    def do_GET(self) -> None:  # noqa: N802
        parsed = urlparse(self.path)
        route = parsed.path.rstrip("/") or "/"
        qs = parse_qs(parsed.query)
        try:
            if route in ("/", "/health"):
                self._send_json({"status": "ok", "service": self.server_version,
                                 "sessions": len(_SESSIONS)})
            elif route == "/teams":
                self._send_json({"teams": all_teams()})
            elif route == "/simulate":
                self._send_json(run_simulation(
                    seed=self._i(qs, "seed"), difficulty=self._f(qs, "difficulty", 0.6),
                    home_key=self._s(qs, "home", DEFAULT_HOME),
                    away_key=self._s(qs, "away", DEFAULT_AWAY),
                    injury_rate=self._f(qs, "injury_rate", 0.0005),
                    include_play_by_play=self._s(qs, "pbp", "1").lower() not in ("0", "false", "no")))
            elif route == "/start":
                self._send_json(start_session(
                    seed=self._i(qs, "seed"), difficulty=self._f(qs, "difficulty", 0.6),
                    home_key=self._s(qs, "home", DEFAULT_HOME),
                    away_key=self._s(qs, "away", DEFAULT_AWAY),
                    injury_rate=self._f(qs, "injury_rate", 0.0005)))
            elif route == "/possession":
                self._send_json(step_session(self._s(qs, "session", "")))
            elif route == "/state":
                self._send_json(session_state(self._s(qs, "session", "")))
            elif route == "/series":
                self._send_json(simulate_series(
                    self._s(qs, "a", DEFAULT_HOME).upper(), self._s(qs, "b", DEFAULT_AWAY).upper(),
                    best_of=self._i(qs, "best_of") or 7, seed=self._i(qs, "seed"),
                    difficulty=self._f(qs, "difficulty", 0.6),
                    injury_rate=self._f(qs, "injury_rate", 0.0006)))
            elif route == "/bracket":
                keys = [k.strip().upper() for k in self._s(qs, "teams", "").split(",") if k.strip()]
                self._send_json(simulate_bracket(
                    keys, seed=self._i(qs, "seed"), best_of=self._i(qs, "best_of") or 7,
                    difficulty=self._f(qs, "difficulty", 0.6),
                    injury_rate=self._f(qs, "injury_rate", 0.0006)))
            else:
                self._send_json({"error": f"unknown route '{route}'"}, status=404)
        except (KeyError, ValueError) as exc:
            self._send_json({"error": str(exc)}, status=400)
        except Exception as exc:  # pragma: no cover
            self._send_json({"error": f"internal: {exc}"}, status=500)

    def log_message(self, fmt: str, *args: Any) -> None:
        pass


def serve(port: int = 8765, host: str = "127.0.0.1") -> None:
    httpd = ThreadingHTTPServer((host, port), _SimHandler)
    print(f"[sim_service] serving on http://{host}:{port}")
    for line in ("/health", "/teams",
                 "/simulate?seed=2005&home=SAS&away=DET",
                 "/start?seed=7&home=BOS&away=DEN  ->  /possession?session=ID",
                 "/series?a=BOS&b=DEN&best_of=7",
                 "/bracket?teams=BOS,DEN,OKC,MIL,DAL,MIN,SAS,DET"):
        print(f"[sim_service]   GET {line}")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n[sim_service] shutting down")
        httpd.shutdown()


def main() -> None:
    parser = argparse.ArgumentParser(description="Bball simulation service")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--once", action="store_true",
                        help="Print one simulation as JSON and exit (subprocess mode)")
    parser.add_argument("--seed", type=int, default=None)
    parser.add_argument("--difficulty", type=float, default=0.6)
    parser.add_argument("--home", default=DEFAULT_HOME)
    parser.add_argument("--away", default=DEFAULT_AWAY)
    parser.add_argument("--no-pbp", action="store_true")
    args = parser.parse_args()

    if args.once:
        print(json.dumps(run_simulation(
            seed=args.seed, difficulty=args.difficulty,
            home_key=args.home.upper(), away_key=args.away.upper(),
            include_play_by_play=not args.no_pbp)))
    else:
        serve(port=args.port, host=args.host)


if __name__ == "__main__":
    main()
