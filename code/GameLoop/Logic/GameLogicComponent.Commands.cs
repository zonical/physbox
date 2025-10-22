using Sandbox;

public partial class GameLogicComponent
{
	[ConCmd( "pb_set_gamemode", ConVarFlags.Server )]
	public static void SetGameModeCommand( int input )
	{
		if ( !Networking.IsHost ) return;

		var GameMode = (PhysboxConstants.GameModes)input;
		if ( GameMode >= PhysboxConstants.GameModes.MAX_GAMEMODE )
		{
			Log.Warning( "Invalid gamemode selected, defaulting to Deathmatch." );
			GameMode = PhysboxConstants.GameModes.Deathmatch;
		}

		GetGameInstance().GameObject.GetComponent<BaseGameMode>().Destroy();
		GetGameInstance().SetGameMode( GameMode );
	}

	[ConCmd( "pb_restart_game", ConVarFlags.Server )]
	public static void RestartGameCommand()
	{
		if ( !Networking.IsHost ) return;

		GetGameInstance().RestartGame();
	}

	[ConCmd( "pb_load_testbed", ConVarFlags.Server )]
	public static void LoadTestbed()
	{
		if ( !Networking.IsHost ) return;

		var slo = new SceneLoadOptions();
		slo.SetScene( "scenes/maps/testbed.scene" );
		slo.DeleteEverything = true;
		Game.ChangeScene( slo );
	}

	[ConCmd( "pb_round_end", ConVarFlags.Admin )]
	public static void EndRound( Connection caller )
	{
		Game.ActiveScene.RunEvent<IGameEvents>( x => x.OnRoundEnd() );
	}
}
