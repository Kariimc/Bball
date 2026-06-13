# =============================================================================
#                               SimClient.gd
# =============================================================================
# Godot 4 client for the Python Bball sim service (sim_service.py).
#
# Architecture: the Python engine is deterministic, so we fetch a whole game
# ONCE, then replay it locally. Godot never re-queries per frame -- it walks the
# `play_by_play` array and animates each possession at its own pace, driving the
# scoreboard/clock UI from the telemetry fields.
#
# SETUP
#   1. Start the service:  python sim_service.py --port 8765
#   2. Add this script to a Node (an HTTPRequest child is created automatically).
#   3. Call simulate(2005, 0.7, "SAS", "DET").
#
# SIGNALS
#   game_loaded(summary)        -> fired once the full game JSON arrives
#   possession_played(play)     -> fired per possession during replay()
#   game_finished(summary)      -> fired when replay() reaches the end
# =============================================================================
extends Node

signal game_loaded(summary: Dictionary)
signal possession_played(play: Dictionary)
signal game_finished(summary: Dictionary)

@export var base_url: String = "http://127.0.0.1:8765"
## Seconds of wall-clock time per simulated possession during replay().
@export var seconds_per_possession: float = 0.6

var _http: HTTPRequest
var _last_game: Dictionary = {}


func _ready() -> void:
	_http = HTTPRequest.new()
	add_child(_http)
	_http.request_completed.connect(_on_request_completed)


# -- request a full game -------------------------------------------------------
func simulate(seed: int = -1, difficulty: float = 0.6,
		home: String = "SAS", away: String = "DET") -> void:
	var query := "difficulty=%f&home=%s&away=%s" % [difficulty, home, away]
	if seed >= 0:
		query += "&seed=%d" % seed
	var url := "%s/simulate?%s" % [base_url, query]
	var err := _http.request(url)
	if err != OK:
		push_error("SimClient: request failed to start (%d)" % err)


func _on_request_completed(_result: int, response_code: int,
		_headers: PackedStringArray, body: PackedByteArray) -> void:
	if response_code != 200:
		push_error("SimClient: HTTP %d" % response_code)
		return
	var parsed: Variant = JSON.parse_string(body.get_string_from_utf8())
	if typeof(parsed) != TYPE_DICTIONARY:
		push_error("SimClient: malformed response")
		return
	_last_game = parsed
	game_loaded.emit(_last_game.get("summary", {}))


# -- replay the fetched game possession-by-possession --------------------------
func replay() -> void:
	if _last_game.is_empty():
		push_error("SimClient: no game loaded -- call simulate() first")
		return
	var plays: Array = _last_game.get("play_by_play", [])
	for play in plays:
		possession_played.emit(play)
		# Example: read telemetry to update your UI here.
		#   play["clock"]    -> "9:49"  (game-clock string)
		#   play["score"]    -> {"HOME": 2, "AWAY": 5}
		#   play["offense"]  -> "San Antonio Spurs"
		#   play["events"]   -> ["Tim Duncan scores 2", "assist Brent Barry"]
		#   play["shot"]     -> {shooter, zone, distance_ft, is_dunk, probability}
		await get_tree().create_timer(seconds_per_possession).timeout
	game_finished.emit(_last_game.get("summary", {}))


func get_summary() -> Dictionary:
	return _last_game.get("summary", {})
