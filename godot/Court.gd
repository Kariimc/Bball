# =============================================================================
#                                 Court.gd
# =============================================================================
# 3D court controller for the placeholder front-end. Spawns 5 placeholder
# players per team in a half-court formation, drives the scoreboard from the
# live-step telemetry, and animates the ball to each shooter.
#
# SWAPPING IN YOUR MODELS:
#   Assign your own character scene to `player_scene` in the inspector (or
#   replace Player.tscn). It only needs a `setup(name, number, color)` method;
#   the controller positions it. Court coordinates are in FEET
#   (x: 0-50 width, z: 0-94 length); _court_to_world() maps them to the scene.
# =============================================================================
extends Node3D

@export var player_scene: PackedScene = preload("res://Player.tscn")
@export var home_team: String = "BOS"
@export var away_team: String = "DEN"
@export var match_seed: int = 7
@export var difficulty: float = 0.6
@export var step_interval: float = 0.5

const HOME_COLOR := Color(0.12, 0.45, 0.95)
const AWAY_COLOR := Color(0.95, 0.30, 0.20)

# Half-court formations by role, in court feet. Home attacks the near hoop
# (z ~ 5); away is set up across the floor.
const HOME_FORM := {
	"PG": Vector2(25, 30), "SG": Vector2(40, 22), "SF": Vector2(10, 22),
	"PF": Vector2(33, 13), "C": Vector2(25, 9),
}
const AWAY_FORM := {
	"PG": Vector2(25, 64), "SG": Vector2(10, 72), "SF": Vector2(40, 72),
	"PF": Vector2(17, 82), "C": Vector2(25, 86),
}

@onready var sim: SimClient = $Sim
@onready var step_timer: Timer = $StepTimer
@onready var ball: Node3D = $Ball
@onready var cam: Camera3D = $Camera3D
@onready var sun: DirectionalLight3D = $Sun

var _players := {}   # player name -> Node3D


func _ready() -> void:
	cam.look_at(Vector3(0, 0, -6), Vector3.UP)
	sun.rotation_degrees = Vector3(-55, -35, 0)
	sim.session_started.connect(_on_session)
	sim.possession_played.connect(_on_possession)
	sim.game_finished.connect(_on_finished)
	step_timer.wait_time = step_interval
	step_timer.timeout.connect(sim.step)
	sim.start_session(match_seed, difficulty, home_team, away_team)


func _court_to_world(x: float, z: float) -> Vector3:
	return Vector3(x - 25.0, 0.0, z - 47.0)


func _on_session(info: Dictionary) -> void:
	$UI/HomeName.text = info["home"]["name"]
	$UI/AwayName.text = info["away"]["name"]
	_spawn(info["home"]["lineup"], HOME_FORM, HOME_COLOR)
	_spawn(info["away"]["lineup"], AWAY_FORM, AWAY_COLOR)
	$UI/PlayFeed.append_text("TIP-OFF: %s vs %s\n" % [info["home"]["name"], info["away"]["name"]])
	step_timer.start()


func _spawn(lineup: Array, form: Dictionary, color: Color) -> void:
	var used := {}
	var num := 1
	for p in lineup:
		var node := player_scene.instantiate()
		add_child(node)
		var role: String = p.get("role", "SF")
		var spot: Vector2 = form.get(role, Vector2(25, 47))
		if used.has(role):
			spot += Vector2(5.0 * used[role], 0)   # nudge duplicate roles
		used[role] = used.get(role, 0) + 1
		node.position = _court_to_world(spot.x, spot.y)
		if node.has_method("setup"):
			node.setup(p["name"], num, color)
		_players[p["name"]] = node
		num += 1


func _on_possession(play: Dictionary) -> void:
	if play.has("score"):
		$UI/HomeScore.text = str(play["score"]["HOME"])
		$UI/AwayScore.text = str(play["score"]["AWAY"])
	if play.has("clock"):
		$UI/Clock.text = play["clock"]
	if play.has("quarter"):
		$UI/Quarter.text = "Q%d" % play["quarter"]

	# Animate the ball to the shooter, if any.
	if play.has("shot"):
		var shooter: String = play["shot"].get("shooter", "")
		if _players.has(shooter):
			var target: Vector3 = _players[shooter].position + Vector3(0, 6, 0)
			create_tween().tween_property(ball, "position", target, 0.3)

	var events: Array = play.get("events", [])
	if not events.is_empty():
		$UI/PlayFeed.append_text("Q%s %s  %s\n" % [
			play.get("quarter", "?"), play.get("clock", ""),
			", ".join(PackedStringArray(events))])


func _on_finished(summary: Dictionary) -> void:
	step_timer.stop()
	var fs: Dictionary = summary["final_score"]
	$UI/PlayFeed.append_text("\nFINAL  %d - %d  ->  %s\n" % [
		fs["HOME"], fs["AWAY"], summary.get("winner", "")])
