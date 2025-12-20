public partial class GameLogicComponent
{
	// Current active gamemode. This is hidden, but doesn't automatically update. Call SetGameMode() to change the value.
	[ConVar( "pb_gamemode", ConVarFlags.Replicated )]
	public static GameModes GameMode { get; set; }

	// When a round ends (firing OnRoundEnd() event), this is the amount of time before StartGame() is called.
	[ConVar( "pb_round_intermission", Help = "How long the intermission between rounds should last.", Max = 10,
		Min = 10 )]
	public static int RoundIntermissionSeconds { get; set; } = 5;

	[ConVar( "pb_use_timer", ConVarFlags.Replicated, Help = "How long the intermission between rounds should last." )]
	public static bool UseTimer { get; set; } = false;

	[ConVar( "pb_use_teams", ConVarFlags.Replicated, Help = "Whether this game should use teams." )]
	public static bool UseTeams { get; set; } = false;

	[ConVar( "pb_friendlyfire", ConVarFlags.Replicated,
		Help = "If we are assigned to teams, are we allowed to damage our teammates?" )]
	public static bool FriendlyFire { get; set; } = false;

	[ConVar( "pb_use_fall_damage", ConVarFlags.Replicated, Help = "If fall damage should be dealt." )]
	public static bool UseFallDamage { get; set; } = true;

	[ConVar( "pb_timer_seconds", ConVarFlags.Replicated, Help = "How long a round lasts in seconds." )]
	public static int TimerLengthInSeconds { get; set; } = 300;

	[ConVar( "pb_maxbots", ConVarFlags.GameSetting, Help = "The maximum amount of bots in this game", Max = 63,
		Min = 0 )]
	public static int MaxBots { get; set; } = 7;


	[ConCmd( "pb_changegamemode" )]
	public static void ChangeGameMode( Connection caller, GameModes newGameMode )
	{
		if ( caller != Connection.Host )
		{
			return;
		}

		var game = GetGameInstance();
		game?.SetGameMode( newGameMode );
	}
}
