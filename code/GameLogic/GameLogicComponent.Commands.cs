public partial class GameLogicComponent
{
	[ConCmd( "pb_set_gamemode", ConVarFlags.Server )]
	public static void SetGameModeCommand( int input )
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		GameMode = (GameModes)input;
		if ( GameMode >= GameModes.MAX_GAMEMODE )
		{
			Log.Warning( "Invalid gamemode selected, defaulting to Deathmatch." );
			GameMode = GameModes.Deathmatch;
		}

		GetGameInstance().GameObject.GetComponent<BaseGameMode>().Destroy();
		GetGameInstance().SetGameMode( GameMode );
	}

	[ConCmd( "pb_restart_game", ConVarFlags.Server )]
	public static void StartGameCommand()
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		GetGameInstance().StartGame();
	}

	[ConCmd( "pb_load_testbed", ConVarFlags.Server )]
	public static void LoadTestbed()
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		var slo = new SceneLoadOptions();
		slo.SetScene( "scenes/maps/testbed.scene" );
		slo.DeleteEverything = true;
		Game.ChangeScene( slo );
	}

	[ConCmd( "pb_round_end", ConVarFlags.Admin )]
	public static void EndRound( Connection caller )
	{
		Game.ActiveScene.RunEvent<IPhysboxGameEvents>( x => x.OnRoundEnd() );
	}

	[ConCmd( "pb_debug_teams_dump", ConVarFlags.Cheat )]
	public static void DumpTeams( Connection caller )
	{
		foreach ( var player in Game.ActiveScene.GetAllComponents<PlayerComponent>()
			         .OrderBy( x => x.Team ) )
		{
			var name = player.IsPlayer ? player.Network.Owner.DisplayName : player.BotName;
			Log.Info( $"{name} - team {player.Team}" );
		}
	}
}
