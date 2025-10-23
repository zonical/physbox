using Sandbox;
using System;
using System.Threading.Tasks;

public static partial class PhysboxConstants
{
	public enum GameModes
	{
		[Hide]
		None = 0,

		Deathmatch = 1,
		//Dodgeball = 2,
		//Instagib = 3,

		[Hide]
		MAX_GAMEMODE
	}
}

public partial class GameLogicComponent
{

	// Current active gamemode. This is hidden, but doesn't automatically update. Call SetGameMode() to change the value.
	[ConVar( "pb_gamemode", ConVarFlags.Replicated | ConVarFlags.Hidden ),
		Group( "Gameplay" )]
	public static PhysboxConstants.GameModes GameMode { get; set; } = PhysboxConstants.GameModes.Deathmatch;

	// When a round ends (firing OnRoundEnd() event), this is the amount of time before RestartGame() is called.
	[ConVar( "pb_round_intermission",
		ConVarFlags.GameSetting,
		Help = "How long the intermission between rounds should last." ),
		Group( "Gameplay" ),
		Range( 0, 10.0f ),
		Title( "Intermission between rounds (seconds)" )]
	public static int RoundIntermissionSeconds { get; set; } = 5;

	// If this game is using a timer, we will fire OnRoundEnd() once it ends.
	// This value is set in each gamemode component.
	[ConVar( "pb_use_timer",
		ConVarFlags.GameSetting | ConVarFlags.Replicated,
		Help = "How long the intermission between rounds should last." ),
		Group( "Gameplay" )]
	public static bool UseTimer { get; set; } = false;

	[ConVar( "pb_timer_seconds",
		ConVarFlags.GameSetting | ConVarFlags.Replicated,
		Help = "How long a round lasts in seconds." ),
		Group( "Gameplay" ),
		Title( "Round length (seconds)" )]
	public static int TimerLengthInSeconds { get; set; } = 300;
}
