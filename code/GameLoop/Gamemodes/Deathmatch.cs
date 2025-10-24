using Sandbox;
using System;
using static PlayerComponent;

[Hide]
public class DeathmatchGameMode : BaseGameMode, IGameEvents
{
	[ConVar( "pb_deathmatch_kills_to_win",
		ConVarFlags.Server | ConVarFlags.Replicated ),
		Group( "Deathmatch" )]
	public static int DeathmatchKillsToWin { get; set; } = 10;

	[Rpc.Broadcast]
	void IGameEvents.OnPlayerDeath( GameObject victim, DamageInfo info )
	{
		if ( RoundOver ) return;

		var victimPlayer = victim.GetComponent<PlayerComponent>();
		if ( victimPlayer is null ) return;

		victimPlayer.Deaths++;
		var attacker = info.Attacker;

		// Add kills to attacking player.
		if ( attacker.Components.TryGet<PlayerComponent>( out var attackerPlayer ) && attackerPlayer != victimPlayer )
		{
			attackerPlayer.Kills++;
			Scene.RunEvent<IGameEvents>( x => x.OnPlayerScoreUpdate( attacker, attackerPlayer.Kills ) );
		}

		victimPlayer.RequestSpawn();
	}

	[Rpc.Broadcast]
	void IGameEvents.OnPlayerScoreUpdate( GameObject player, int score )
	{
		if ( RoundOver ) return;

		//Log.Info( "DeathmatchGameMode.OnPlayerScoreUpdate()" );
		if ( score >= DeathmatchKillsToWin )
		{
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
