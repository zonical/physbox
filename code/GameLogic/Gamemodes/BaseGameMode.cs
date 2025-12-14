using Sandbox;
using System;

public class PhysboxGamemodeAttribute( GameModes gameMode ) : Attribute
{
	public GameModes GameMode = gameMode;
}

[Hide]
public class BaseGameMode : Component
{
	[Sync] [Property] [ReadOnly] public int RoundsPlayed { get; set; } = 0;
	[Sync] [Property] [ReadOnly] public bool RoundOver { get; set; } = false;
	[Property] public PlayerComponent WinningPlayer { get; set; }
	[Property] public Team WinningTeam { get; set; }

	public bool HasWinner => WinningTeam != Team.None || WinningPlayer is not null;

	protected GameLogicComponent Game => GameLogicComponent.GetGameInstance();

	public virtual void OnRoundStart()
	{
		RoundsPlayed++;
		RoundOver = false;
		WinningPlayer = null;
		WinningTeam = Team.None;
	}

	public virtual void OnRoundEnd()
	{
		RoundOver = true;
	}

	[Rpc.Broadcast]
	public void DeclareWinner( PlayerComponent player )
	{
		WinningPlayer = player;
	}

	[Rpc.Broadcast]
	public void DeclareWinner( Team team )
	{
		WinningTeam = team;
	}


	[ConCmd( "pb_debug_declare_me_winner", ConVarFlags.Cheat )]
	public static void DeclareMeWinner( Connection caller )
	{
		var game = GameLogicComponent.GetGameInstance();
		var player = Sandbox.Game.ActiveScene
			.GetAllComponents<PlayerComponent>().First( x => x.Network.OwnerId == caller.Id && x.IsPlayer );

		game.GameModeComponent.DeclareWinner( player );
	}

	[ConCmd( "pb_debug_declare_team_winner", ConVarFlags.Cheat )]
	public static void DeclareTeamWinner( Connection caller, Team team )
	{
		var game = GameLogicComponent.GetGameInstance();
		game.GameModeComponent.DeclareWinner( team );
	}
}
