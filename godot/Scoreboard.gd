# =============================================================================
#                              Scoreboard.gd
# =============================================================================
# Minimal visual proof-of-concept that drives a scoreboard + play feed from the
# Python sim service. Demonstrates the LIVE STEP loop: it asks the service for
# one possession at a time and renders the telemetry. Swap to fetch_game()/
# replay() for the deterministic replay flow.
#
# Wired up by Main.tscn. Nodes expected as children of this Control:
#   AwayName, AwayScore, Clock, Quarter, HomeName, HomeScore  (Labels)
#   PlayFeed (RichTextLabel)   PossessionTimer (Timer)        Sim (SimClient)
# =============================================================================
extends Control

@export var home_team: String = "BOS"
@export var away_team: String = "DEN"
@export var seed: int = 7
@export var difficulty: float = 0.6
## Seconds between possessions in live-step mode.
@export var step_interval: float = 0.5

@onready var sim: SimClient = $Sim
@onready var away_name: Label = $AwayName
@onready var away_score: Label = $AwayScore
@onready var home_name: Label = $HomeName
@onready var home_score: Label = $HomeScore
@onready var clock: Label = $Clock
@onready var quarter: Label = $Quarter
@onready var feed: RichTextLabel = $PlayFeed
@onready var timer: Timer = $PossessionTimer


func _ready() -> void:
	sim.session_started.connect(_on_session_started)
	sim.possession_played.connect(_on_possession)
	sim.game_finished.connect(_on_game_finished)
	timer.wait_time = step_interval
	timer.timeout.connect(sim.step)
	sim.start_session(seed, difficulty, home_team, away_team)


func _on_session_started(info: Dictionary) -> void:
	home_name.text = info["home"]["name"]
	away_name.text = info["away"]["name"]
	home_score.text = "0"
	away_score.text = "0"
	_log("[b]TIP-OFF[/b] -- %s vs %s" % [info["home"]["name"], info["away"]["name"]])
	timer.start()


func _on_possession(play: Dictionary) -> void:
	# Update scoreboard from telemetry.
	if play.has("score"):
		home_score.text = str(play["score"]["HOME"])
		away_score.text = str(play["score"]["AWAY"])
	if play.has("clock"):
		clock.text = play["clock"]
	if play.has("quarter"):
		quarter.text = "Q%d" % play["quarter"]

	# Only surface meaningful events in the feed.
	var events: Array = play.get("events", [])
	if not events.is_empty():
		_log("Q%s %s  %s" % [play.get("quarter", "?"), play.get("clock", ""),
				", ".join(PackedStringArray(events))])


func _on_game_finished(summary: Dictionary) -> void:
	timer.stop()
	var fs: Dictionary = summary.get("final_score", {})
	_log("\n[b]FINAL[/b]: %s %d - %d %s  ->  %s" % [
			summary["home"]["name"], fs.get("HOME", 0),
			fs.get("AWAY", 0), summary["away"]["name"], summary.get("winner", "")])
	for inj in summary.get("injuries", []):
		_log("[color=red]INJ[/color] %s (%s)" % [inj["player"], inj["note"]])


func _log(line: String) -> void:
	feed.append_text(line + "\n")
