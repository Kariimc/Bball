# =============================================================================
#                                 Player.gd
# =============================================================================
# Placeholder player. The capsule + name tag are PLACEHOLDERS -- replace the
# "Mesh" MeshInstance3D (or swap the whole Player.tscn via Court.gd's
# `player_scene` export) with your own imported 3D character model. Keep the
# setup() method so the controller can still name/tint each player.
# =============================================================================
extends Node3D


func setup(player_name: String, number: int, team_color: Color) -> void:
	var label := get_node_or_null("Label3D")
	if label and label is Label3D:
		label.text = "#%d  %s" % [number, player_name]
		label.modulate = team_color.lightened(0.3)

	# Tint the placeholder body. (Your real model can ignore this.)
	var mesh := get_node_or_null("Mesh")
	if mesh and mesh is MeshInstance3D:
		var mat := StandardMaterial3D.new()
		mat.albedo_color = team_color
		mesh.material_override = mat
