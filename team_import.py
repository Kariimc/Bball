# ==============================================================================
#                               TEAM_IMPORT.PY
# ==============================================================================
# Import custom teams from a URL (or local file) as JSON, validate + normalize
# the data, and register them so the engine / series / service can use them by
# key -- exactly like the built-in 2005 and modern rosters.
#
# JSON SCHEMA (see examples/team_template.json):
#   {
#     "key": "LAL",                       # optional short key (<= 5 chars)
#     "name": "Los Angeles Lakers",       # team display name
#     "players": [
#       {
#         "name": "Point Guard",          # required
#         "role": "PG",                   # PG|SG|SF|PF|C (default SF)
#         "height_inches": 75,            # 60..96 (default 78)
#         "stats": { "three_point": 82, ... }   # any engine stat key, 0..100
#       },
#       ...  (>= 5 players to field a lineup; >= 8 recommended)
#     ]
#   }
# A bare JSON list of players is also accepted (name supplied separately).
#
# SECURITY: fetching a user-supplied URL server-side is an SSRF vector. By
# default we refuse non-public targets (loopback / private / link-local), cap the
# response size, and time out. Pass allow_private=True only for trusted local dev.
# ==============================================================================

from __future__ import annotations
import ipaddress
import json
import socket
from typing import Any, Dict, List, Optional, Tuple
from urllib.parse import urlparse
from urllib.request import Request, urlopen

from nba_comprehensive_game_engine import DEFAULT_STATS
from rosters import register_team

VALID_ROLES = ("PG", "SG", "SF", "PF", "C")
MAX_PLAYERS = 20
MIN_PLAYERS = 5
MAX_BYTES = 1_000_000
DEFAULT_TIMEOUT = 6.0


# ----------------------------------------------------------- validation


def _clamp(value: Any, lo: float, hi: float, fallback: float) -> int:
    try:
        return int(max(lo, min(hi, float(value))))
    except (TypeError, ValueError):
        return int(fallback)


def normalize_player(raw: Dict[str, Any]) -> Dict[str, Any]:
    if not isinstance(raw, dict):
        raise ValueError("each player must be a JSON object")
    name = str(raw.get("name", "")).strip()
    if not name:
        raise ValueError("every player needs a non-empty 'name'")

    role = str(raw.get("role", "SF")).upper()
    if role not in VALID_ROLES:
        role = "SF"

    height = _clamp(raw.get("height_inches", 78.0), 60, 96, 78)

    stats_in = raw.get("stats", {}) or {}
    if not isinstance(stats_in, dict):
        raise ValueError(f"'stats' for {name} must be an object")
    stats: Dict[str, int] = {}
    for key, val in stats_in.items():
        if key not in DEFAULT_STATS:
            continue  # ignore unknown keys rather than trusting them blindly
        if key == "vertical_leap":
            stats[key] = _clamp(val, 15, 50, DEFAULT_STATS[key])
        else:
            stats[key] = _clamp(val, 0, 100, DEFAULT_STATS[key])

    return {"name": name, "role": role, "height_inches": float(height), "stats": stats}


def validate_roster(data: Any, name: Optional[str] = None,
                    key: Optional[str] = None) -> Dict[str, Any]:
    """Validate parsed JSON into a {key, name, players} payload."""
    if isinstance(data, list):
        players_raw = data
        team_name = name or "Imported Team"
        team_key = key
    elif isinstance(data, dict):
        players_raw = data.get("players")
        team_name = str(data.get("name") or name or "Imported Team")
        team_key = key or data.get("key")
        if players_raw is None:
            raise ValueError("team object must contain a 'players' list")
    else:
        raise ValueError("top-level JSON must be a team object or a list of players")

    if not isinstance(players_raw, list) or not players_raw:
        raise ValueError("'players' must be a non-empty list")
    if len(players_raw) > MAX_PLAYERS:
        raise ValueError(f"too many players ({len(players_raw)} > {MAX_PLAYERS})")

    players = [normalize_player(p) for p in players_raw]
    if len(players) < MIN_PLAYERS:
        raise ValueError(f"need at least {MIN_PLAYERS} players to field a lineup")

    roles = {p["role"] for p in players}
    missing = [r for r in VALID_ROLES if r not in roles]
    warnings: List[str] = []
    if missing:
        warnings.append(f"no players at position(s) {missing}; lineup will backfill by rating")

    if team_key:
        team_key = str(team_key).upper()[:5]

    return {"key": team_key, "name": team_name, "players": players, "warnings": warnings}


# ----------------------------------------------------------- SSRF guard


def _assert_public_url(url: str, allow_private: bool) -> str:
    parsed = urlparse(url)
    if parsed.scheme not in ("http", "https"):
        raise ValueError(f"unsupported URL scheme '{parsed.scheme}' (use http/https)")
    host = parsed.hostname
    if not host:
        raise ValueError("URL has no host")
    if allow_private:
        return url
    try:
        infos = socket.getaddrinfo(host, parsed.port or (443 if parsed.scheme == "https" else 80))
    except socket.gaierror as exc:
        raise ValueError(f"cannot resolve host '{host}': {exc}") from exc
    for info in infos:
        ip = ipaddress.ip_address(info[4][0])
        if (ip.is_private or ip.is_loopback or ip.is_link_local
                or ip.is_reserved or ip.is_multicast or ip.is_unspecified):
            raise ValueError(
                f"refusing to fetch from non-public address {ip} "
                "(pass allow_private=True only for trusted local dev)")
    return url


# ----------------------------------------------------------- loaders


def load_team_data_from_url(url: str, *, timeout: float = DEFAULT_TIMEOUT,
                            max_bytes: int = MAX_BYTES, allow_private: bool = False,
                            name: Optional[str] = None,
                            key: Optional[str] = None) -> Dict[str, Any]:
    _assert_public_url(url, allow_private)
    req = Request(url, headers={"User-Agent": "BballSim/2.0", "Accept": "application/json"})
    with urlopen(req, timeout=timeout) as resp:  # noqa: S310 (scheme already vetted)
        raw = resp.read(max_bytes + 1)
    if len(raw) > max_bytes:
        raise ValueError(f"response exceeds {max_bytes} bytes")
    try:
        data = json.loads(raw.decode("utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        raise ValueError(f"invalid JSON from URL: {exc}") from exc
    return validate_roster(data, name=name, key=key)


def load_team_data_from_file(path: str, *, name: Optional[str] = None,
                             key: Optional[str] = None) -> Dict[str, Any]:
    with open(path, "r", encoding="utf-8") as fh:
        data = json.load(fh)
    return validate_roster(data, name=name, key=key)


# ----------------------------------------------------------- registration


def register_imported_team(payload: Dict[str, Any], fallback_key: Optional[str] = None) -> str:
    """Register a validated payload into the global roster registry; returns the
    key under which it can be looked up (e.g. by simulate_game / series)."""
    key = payload.get("key") or fallback_key
    if not key:
        # Derive a key from the name if none supplied.
        key = "".join(c for c in payload["name"].upper() if c.isalnum())[:4] or "CUST"
    key = register_team(key, payload["name"], payload["players"])
    return key


def import_team_from_url(url: str, *, allow_private: bool = False,
                         key: Optional[str] = None, name: Optional[str] = None,
                         timeout: float = DEFAULT_TIMEOUT) -> Dict[str, Any]:
    """Fetch + validate + register a team from a URL. Returns a small summary."""
    payload = load_team_data_from_url(url, allow_private=allow_private,
                                      timeout=timeout, name=name, key=key)
    registered_key = register_imported_team(payload, fallback_key=key)
    return {"key": registered_key, "name": payload["name"],
            "players": len(payload["players"]), "warnings": payload["warnings"]}


# ----------------------------------------------------------- CLI


def main() -> None:
    import argparse
    parser = argparse.ArgumentParser(description="Validate / import a team JSON")
    src = parser.add_mutually_exclusive_group(required=True)
    src.add_argument("--url", help="fetch team JSON from this URL")
    src.add_argument("--file", help="read team JSON from this local file")
    parser.add_argument("--allow-private", action="store_true",
                        help="permit loopback/private URLs (trusted local dev only)")
    parser.add_argument("--name", help="override team name")
    parser.add_argument("--key", help="override team key")
    args = parser.parse_args()

    if args.url:
        payload = load_team_data_from_url(args.url, allow_private=args.allow_private,
                                          name=args.name, key=args.key)
    else:
        payload = load_team_data_from_file(args.file, name=args.name, key=args.key)

    print(f"OK: {payload['name']}  ({len(payload['players'])} players)"
          f"  key={payload['key']}")
    for w in payload["warnings"]:
        print(f"  warning: {w}")
    for p in payload["players"]:
        print(f"   {p['role']:2s}  {p['name']:<22}  {p['height_inches']:.0f}in  "
              f"{len(p['stats'])} stats")


if __name__ == "__main__":
    main()
