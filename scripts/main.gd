extends Node3D

var clipmap_tile_size := 1.0 # Not the smallest tile size, but one that reduces the amount of vertex jitter.
var previous_tile := Vector3i.MAX

@onready var viewport : Variant = Engine.get_singleton(&'EditorInterface').get_editor_viewport_3d(0) if Engine.is_editor_hint() else get_viewport()
@onready var camera : Variant = get_viewport().get_camera_3d()
@onready var water := $Water
@onready var player := $"../Player"
@onready var game_over := $"../UI/GameOver"
@onready var flooded := $"../UI/GameOver/FLOODED"
@onready var restart := $"../UI/GameOver/Restart"
@onready var tint := $"../UI/GameOver/Tint"

func _init() -> void:
	if Engine.is_editor_hint(): return
	if DisplayServer.window_get_vsync_mode() == DisplayServer.VSYNC_ENABLED:
		DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	DisplayServer.window_set_size(DisplayServer.screen_get_size() * 0.75)
	DisplayServer.window_set_position(DisplayServer.screen_get_size() * 0.25 / 2.0)

func _physics_process(_delta: float) -> void:
	# Shift water mesh whenever player moves into a new tile.
	var tile := (
		Vector3(
			camera.global_position.x, 
			0, 
			camera.global_position.z
		) / clipmap_tile_size).ceil()
	if not tile.is_equal_approx(previous_tile):
		water.global_position = tile * clipmap_tile_size + Vector3(0, water.global_position.y, 0)
		previous_tile = tile
	
	# Vary audio samples based on total wind speed across all cascades.
	var total_wind_speed := 0.0
	for params in water.parameters:
		total_wind_speed += params.wind_speed
	
	if player.global_position.y < water.global_position.y + 3:
		player.axis_lock_linear_x = true
		player.axis_lock_linear_y = true
		player.axis_lock_linear_z = true
		player.axis_lock_angular_x = true
		player.axis_lock_angular_x = true
		player.axis_lock_angular_x = true
	
		$OceanAudioPlayer.volume_db = lerpf(-30.0, 5.0, minf(total_wind_speed/15.0, 1.0))
		$WindAudioPlayer.volume_db = lerpf(0.0, -30.0, minf(total_wind_speed/15.0, 1.0))
		$RainAudioPlayer.volume_db = -20
		
		game_over.visible = true
		restart.set("theme_override_colors/font_color", Color(1, 1, 1, 0))
		
		if Input.is_action_just_pressed("reset"):
			water.global_position.y = -35
			player.position = player._resetPosition.position
			
			player.axis_lock_linear_x = false
			player.axis_lock_linear_y = false
			player.axis_lock_linear_z = false
			player.axis_lock_angular_x = false
			player.axis_lock_angular_x = false
			player.axis_lock_angular_x = false
			
			game_over.visible = false
			
	else:
		water.global_position.y += 0.01
	
		$OceanAudioPlayer.volume_db = lerpf(-30.0, 10.0, minf(total_wind_speed/15.0, 1.0))
		$WindAudioPlayer.volume_db = lerpf(5.0, -30.0, minf(total_wind_speed/15.0, 1.0))
		$RainAudioPlayer.volume_db = -10
	
	$Rain.global_position = camera.global_position + Vector3(0, 10, 0)
