public static partial class PhysboxConstants
{
	public enum GameModes
	{
		[Hide] None = 0,

		[Icon( "⚔️" )] Deathmatch = 1,
		//Dodgeball = 2,
		//Instagib = 3,

		[Hide] MAX_GAMEMODE
	}
}

public partial class GameLogicComponent
{
	// Current active gamemode. This is hidden, but doesn't automatically update. Call SetGameMode() to change the value.
	[ConVar( "pb_gamemode", ConVarFlags.Replicated | ConVarFlags.Hidden )]
	public static PhysboxConstants.GameModes GameMode { get; set; } = PhysboxConstants.GameModes.Deathmatch;

	// When a round ends (firing OnRoundEnd() event), this is the amount of time before RestartGame() is called.
	[ConVar( "pb_round_intermission", Help = "How long the intermission between rounds should last.", Max = 10,
		Min = 10 )]
	public static int RoundIntermissionSeconds { get; set; } = 5;
	
	[ConVar( "pb_use_timer", ConVarFlags.Replicated, Help = "How long the intermission between rounds should last." )]
	public static bool UseTimer { get; set; } = false;

	[ConVar( "pb_use_fall_damage", ConVarFlags.Replicated, Help = "If fall damage should be dealt." )]
	public static bool UseFallDamage { get; set; } = true;

	[ConVar( "pb_timer_seconds", ConVarFlags.Replicated, Help = "How long a round lasts in seconds." )]
	public static int TimerLengthInSeconds { get; set; } = 300;

	[ConVar( "pb_maxbots", ConVarFlags.GameSetting, Help = "The maximum amount of bots in this game", Max = 63,
		Min = 0 )]
	public static int MaxBots { get; set; } = 7;

	[ConCmd( "pb_map" )]
	public static void SetMap( Connection caller, string mapName )
	{
		if ( caller != Connection.Host )
		{
			return;
		}

		// String sanity checks.
		if ( !mapName.StartsWith( "scenes/maps" ) )
		{
			mapName = "scenes/maps/" + mapName;
		}

		if ( !mapName.EndsWith( ".scene" ) )
		{
			mapName += ".scene";
		}

		var file = ResourceLibrary.Get<SceneFile>( mapName );
		if ( file is null )
		{
			Log.Error( $"Could not find map {mapName}." );
			return;
		}

		// Start a new lobby if we don't have one yet.
		if ( Networking.IsActive )
		{
			Networking.Disconnect();
		}

		PhysboxUtilites.CreateNewLobby();

		// Change to our new scene.
		var slo = new SceneLoadOptions { ShowLoadingScreen = true };
		slo.SetScene( file );
		Game.ChangeScene( slo );
	}
}
