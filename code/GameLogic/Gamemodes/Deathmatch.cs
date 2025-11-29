using Sandbox;
using System;
using static PlayerComponent;

[Hide]
public class DeathmatchGameMode : BaseGameMode, IGameEvents
{
	[ConVar( "pb_deathmatch_kills_to_win",
		ConVarFlags.GameSetting ),
		Group( "Deathmatch" ),
		Title( "Kills to Win" )]
	public static int DeathmatchKillsToWin { get; set; } = 5;

	[Rpc.Broadcast]
	void IGameEvents.OnPlayerDeath( GameObject victim, DamageInfo info )
	{
		if ( RoundOver ) return;

		var victimPlayer = victim.GetComponent<PlayerComponent>();
		if ( victimPlayer is null ) return;

		victimPlayer.Deaths++;
		PhysboxUtilites.IncrementStatForPlayer( victimPlayer, PhysboxConstants.DeathsStat, 1 );

		var attacker = info.Attacker;

		if ( attacker is not null &&
			attacker.Components.TryGet<PlayerComponent>( out var attackerPlayer ) &&
			attackerPlayer != victimPlayer )
		{
			// Add kills to attacking player.
			attackerPlayer.Kills++;
			PhysboxUtilites.IncrementStatForPlayer( attackerPlayer, PhysboxConstants.KillsStat, 1 );

			Scene.RunEvent<IGameEvents>( x => x.OnPlayerScoreUpdate( attacker, attackerPlayer.Kills ) );
		}

		victimPlayer.RequestSpawn();
	}

	[Rpc.Broadcast]
	void IGameEvents.OnPlayerScoreUpdate( GameObject player, int score )
	{
		if ( RoundOver ) return;

		if ( score >= DeathmatchKillsToWin )
		{
			// We have a winner!
			DeclareWinner( player );
			var chat = ChatManagerComponent.GetChatManager();
			var playerComp = player.GetComponent<PlayerComponent>();
			var name = playerComp.IsPlayer ? playerComp.Network.Owner.DisplayName : playerComp.BotName;

			chat.SendMessage( MessageType.System, $"Round over! {name} wins with {score} kills!" );
			PhysboxUtilites.IncrementStatForPlayer( playerComp, PhysboxConstants.WinsStat, 1 );

			// Hand control back to master logic component.
			Scene.RunEvent<IGameEvents>( x => x.OnRoundEnd() );
			RoundOver = true;
		}
	}

	[Rpc.Broadcast]
	void IGameEvents.OnRoundStart()
	{
		base.OnRoundStart();

		foreach ( var player in Scene.GetAllComponents<PlayerComponent>() )
		{
			if ( player.SpawnCancellationToken.CanBeCanceled )
			{
				player.SpawnCancellationTokenSource.Cancel();
			}

			player.Kills = 0;
			player.Deaths = 0;
			player.Spawn();
		}
	}

	[Rpc.Broadcast]
	void IGameEvents.OnRoundEnd()
	{
		base.OnRoundEnd();

		// Stop players from respawning.
		foreach ( var player in Scene.GetAllComponents<PlayerComponent>() )
		{
			if ( player.SpawnCancellationToken.CanBeCanceled )
			{
				player.SpawnCancellationTokenSource.Cancel();
			}
		}
	}
}
