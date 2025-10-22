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
	void IGameEvents.OnPlayerDeath( PlayerComponent player, GameObject attacker )
	{
		if ( RoundOver ) return;

		player.Deaths++;

		// Add kills to attacking player.
		if ( attacker is not null && attacker.Components.TryGet<PropLifeComponent>( out var prop ) )
		{
			if ( prop.LastOwnedBy is not null )
			{
				if ( prop.LastOwnedBy != player )
				{
					prop.LastOwnedBy.Kills++;
					Scene.RunEvent<IGameEvents>( x => x.OnPlayerScoreUpdate( prop.LastOwnedBy, prop.LastOwnedBy.Kills ) );
				}
				Log.Info( $"{prop.LastOwnedBy.Network.Owner.DisplayName} killed {player.Network.Owner.DisplayName}" );
			}
		}

		player.RequestSpawn();
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
