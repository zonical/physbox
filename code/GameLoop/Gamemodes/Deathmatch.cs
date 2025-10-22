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
	void IGameEvents.OnPlayerDeath( PlayerComponent victim, DamageInfo info )
	{
		if ( RoundOver ) return;

		victim.Deaths++;
		var attacker = info.Attacker;

		// Add kills to attacking player.
		if ( attacker.Components.TryGet<PlayerComponent>( out var attackerPlayer ) && attackerPlayer != victim )
		{
			attackerPlayer.Kills++;

			Scene.RunEvent<IGameEvents>( x => x.OnPlayerScoreUpdate( attackerPlayer, attackerPlayer.Kills ) );

			Log.Info( $"{attacker.Network.Owner.DisplayName} killed {victim.Network.Owner.DisplayName}" );
		}

		victim.RequestSpawn();
	}

	[Rpc.Broadcast]
	void IGameEvents.OnPlayerScoreUpdate( PlayerComponent player, int score )
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
			player.SpawnCancellationTokenSource.Cancel();

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
			player.SpawnCancellationTokenSource.Cancel();
		}
	}
}
