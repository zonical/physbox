using Sandbox;
using System;

public class PhysboxGamemodeAttribute( PhysboxConstants.GameModes gameMode ) : Attribute
{
	public PhysboxConstants.GameModes GameMode = gameMode;
}

[Hide]
public class BaseGameMode : Component
{
	[Sync] [Property] [ReadOnly] public int RoundsPlayed { get; set; } = 0;
	[Sync] [Property] [ReadOnly] public bool RoundOver { get; set; } = false;
	[Property] public GameObject Winner { get; set; }

	protected GameLogicComponent Game => GameLogicComponent.GetGameInstance();

	public virtual void OnRoundStart()
	{
		RoundsPlayed++;
		RoundOver = false;
		Winner = null;
	}

	public virtual void OnRoundEnd()
	{
		RoundOver = true;
	}

	[Rpc.Broadcast]
	public void DeclareWinner( GameObject player )
	{
		Winner = player;
	}

	[ConCmd( "pb_debug_declare_me_winner", ConVarFlags.Cheat )]
	public static void DeclareMeWinner( Connection caller )
	{
		var game = GameLogicComponent.GetGameInstance();
		var player = Sandbox.Game.ActiveScene.GetAllComponents<PlayerComponent>()
			.Where( x => x.Network.OwnerId == caller.Id && x.IsPlayer ).First();

		game.GameModeComponent.DeclareWinner( player.GameObject );
	}
}
