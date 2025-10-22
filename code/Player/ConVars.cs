using Physbox;
using System;

public static class PlayerConvars
{
	[ConVar( "pb_player_respawn_immunity", ConVarFlags.GameSetting | ConVarFlags.Replicated ),
		Group( "Player" )]
	public static float RespawnImmunity { get; set; } = 3.0f;

	[ConVar( "pb_player_respawn_time", ConVarFlags.GameSetting | ConVarFlags.Replicated ),
		Group( "Player" )]
	public static float RespawnTime { get; set; } = 3.0f;

	[ConVar( "pb_player_run_speed", ConVarFlags.Server )]
	public static float RunSpeed { get; set; } = 320;

	[ConVar( "pb_player_walk_speed", ConVarFlags.Server )]
	public static float WalkSpeed { get; set; } = 110;

	[ConVar( "pb_player_duck_speed", ConVarFlags.Server )]
	public static float DuckedSpeed { get; set; } = 240;

	[ConVar( "pb_player_speed_affected_by_mass", ConVarFlags.GameSetting | ConVarFlags.Replicated ),
		Group( "Player" )]
	public static bool SpeedAffectedByMass { get; set; } = true;

	[ConVar( "pb_player_deadtalk", ConVarFlags.GameSetting | ConVarFlags.Replicated ),
		Title( "Talk while Dead" ), Group( "Player" )]
	public static bool Deadtalk { get; set; } = true;

	[ConVar( "pb_debug_draw_player_hitboxes", ConVarFlags.Server,
		Help = "When set to true, draw all player hitboxes." )]
	public static bool DrawPlayerHitboxes { get; set; } = false;

	[ConCmd( "pb_suicide" )]
	public static void DebugSuicide( Connection caller )
	{
		var player = PlayerComponent.LocalPlayer;

		// Find caller in the world.
		if ( player is null )
		{
			Log.Error( "Could not find local player." );
			return;
		}

		var damageinfo = new DamageInfo( 9999, player.GameObject, null );
		player.OnDamage( damageinfo );
	}

	[ConCmd( "pb_respawn", ConVarFlags.Cheat )]
	public static void DebugRespawn( Connection caller )
	{
		var player = PlayerComponent.LocalPlayer;

		// Find caller in the world.
		if ( player is null )
		{
			Log.Error( "Could not find local player." );
			return;
		}

		player.Spawn();
	}

	[ConCmd( "pb_god", ConVarFlags.Cheat )]
	public static void DebugGodMode( Connection caller )
	{
		var player = PlayerComponent.LocalPlayer;

		// Find caller in the world.
		if ( player is null )
		{
			Log.Error( "Could not find local player." );
			return;
		}

		player.GodMode = !player.GodMode;
		Log.Info( $"Godmode set to {player.GodMode}." );
	}

	[ConCmd( "pb_freecam", ConVarFlags.Cheat )]
	public static void DebugFreeCam( Connection caller )
	{
		var player = PlayerComponent.LocalPlayer;

		// Find caller in the world.
		if ( player is null )
		{
			Log.Error( "Could not find local player." );
			return;
		}

		player.FreeCam = !player.FreeCam;
		Log.Info( $"FreeCam set to {player.FreeCam}." );
	}
}
