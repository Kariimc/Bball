# =============================================================================
#                               SimClient.gd
# =============================================================================
# Godot 4 client for the Python Bball sim service (sim_service.py).
#
# Supports BOTH integration modes:
#   * REPLAY   : fetch_game() pulls a whole game; replay() walks play_by_play.
#   * LIVE STEP: start_session() then step() advances one possession at a time.
#
# SETUP
#   1. Start the service:  python sim_service.py --port 8765
#   2. Add this script to a Node (an HTTPRequest child is created automatically).
#
# SIGNALS
#   game_loaded(summary)       -> REPLAY: full game JSON arrived
#   possession_played(play)    -> a possession was produced (either mode)
#   game_finished(summary)     -> game reached its end
#   session_started(info)      -> LIVE: session id + starting lineups
# =============================================================================
extends Node
class_name SimClient

signal game_loaded(summary: Dictionary)
signal possession_played(play: Dictionary)
signal game_finished(summary: Dictionary)
signal session_started(info: Dictionary)
signal teams_listed(teams: Dictionary)
signal team_imported(info: Dictionary)
signal series_done(result: Dictionary)
signal bracket_done(result: Dictionary)

@export var base_url: String = "http://127.0.0.1:8765"
## Seconds of wall-clock time per possession during replay().
@export var seconds_per_possession: float = 0.6

enum Mode { IDLE, REPLAY, STEP }

var _http: HTTPRequest
var _mode: int = Mode.IDLE
var _last_game: Dictionary = {}
var _session: String = ""


func _ready() -> void:
	_http = HTTPRequest.new()
	add_child(_http)
	_http.request_completed.connect(_on_request_completed)


# -- REPLAY mode ---------------------------------------------------------------
func fetch_game(seed: int = -1, difficulty: float = 0.6,
		home: String = "BOS", away: String = "DEN") -> void:
	_mode = Mode.REPLAY
	var q := "difficulty=%f&home=%s&away=%s" % [difficulty, home, away]
	if seed >= 0:
		q += "&seed=%d" % seed
	_get("/simulate?" + q)


func replay() -> void:
	if _last_game.is_empty():
		push_error("SimClient: no game loaded")
		return
	for play in _last_game.get("play_by_play", []):
		possession_played.emit(play)
		await get_tree().create_timer(seconds_per_possession).timeout
	game_finished.emit(_last_game.get("summary", {}))


# -- LIVE STEP mode ------------------------------------------------------------
func start_session(seed: int = -1, difficulty: float = 0.6,
		home: String = "BOS", away: String = "DEN") -> void:
	_mode = Mode.STEP
	var q := "difficulty=%f&home=%s&away=%s" % [difficulty, home, away]
	if seed >= 0:
		q += "&seed=%d" % seed
	_get("/start?" + q)


## Advance exactly one possession. Call again from your game loop / on a timer.
func step() -> void:
	if _session == "":
		push_error("SimClient: no active session -- call start_session() first")
		return
	_get("/possession?session=" + _session)


# -- full feature surface (teams / import / playoffs) --------------------------
## List all available teams (built-in + imported) -> teams_listed signal.
func list_teams() -> void:
	_get("/teams")

## Import a custom team from a URL -> team_imported signal. Use allow_private
## only for trusted localhost JSON during development.
func import_team(url: String, allow_private: bool = false) -> void:
	var q := "url=" + url.uri_encode()
	if allow_private:
		q += "&allow_private=1"
	_get("/import?" + q)

## Run a best-of-N playoff series -> series_done signal.
func run_series(a: String, b: String, best_of: int = 7, series_seed: int = -1) -> void:
	var q := "a=%s&b=%s&best_of=%d" % [a, b, best_of]
	if series_seed >= 0:
		q += "&seed=%d" % series_seed
	_get("/series?" + q)

## Run a single-elimination bracket -> bracket_done signal.
## `team_keys` is a power-of-two list, e.g. ["BOS","DEN","OKC","MIL"].
func run_bracket(team_keys: Array, best_of: int = 7) -> void:
	_get("/bracket?teams=" + ",".join(PackedStringArray(team_keys)) + "&best_of=%d" % best_of)


# -- HTTP plumbing -------------------------------------------------------------
func _get(path: String) -> void:
	var err := _http.request(base_url + path)
	if err != OK:
		push_error("SimClient: request failed to start (%d)" % err)


func _on_request_completed(_result: int, code: int,
		_headers: PackedStringArray, body: PackedByteArray) -> void:
	if code != 200:
		push_error("SimClient: HTTP %d -- %s" % [code, body.get_string_from_utf8()])
		return
	var data: Variant = JSON.parse_string(body.get_string_from_utf8())
	if typeof(data) != TYPE_DICTIONARY:
		push_error("SimClient: malformed response")
		return

	if data.has("play_by_play"):                 # /simulate response
		_last_game = data
		game_loaded.emit(data.get("summary", {}))
	elif data.has("session") and data.has("opening"):  # /start response
		_session = data["session"]
		session_started.emit(data)
	elif data.get("game_over", false):           # final /possession response
		game_finished.emit(data.get("summary", {}))
	else:                                         # a normal possession
		possession_played.emit(data)


func get_summary() -> Dictionary:
	return _last_game.get("summary", {})
