using Sandbox;
using System;
using static PlayerComponent;

[Hide]
[PhysboxGamemode( PhysboxConstants.GameModes.Deathmatch )]
public class DeathmatchGameMode : BaseGameMode, IGameEvents
{
	[ConVar( "pb_deathmatch_kills_to_win",
		ConVarFlags.GameSetting )]
	[Group( "Deathmatch" )]
	[Title( "Kills to Win" )]
	public static int DeathmatchKillsToWin { get; set; } = 5;

	[Sync] private NetDictionary<Team, int> TeamKills { get; set; } = new();

	[Rpc.Broadcast]
	void IGameEvents.OnPlayerDeath( PhysboxDamageInfo info )
	{
		if ( RoundOver || info.Victim is null )
		{
			return;
		}

		info.Victim.Deaths++;
		PhysboxUtilites.IncrementStatForPlayer( info.Victim, PhysboxConstants.DeathsStat, 1 );

		if ( info.Attacker is not null && !info.IsSuicide )
		{
			// Add kills to attacking player.
			info.Attacker.Kills++;
			PhysboxUtilites.IncrementStatForPlayer( info.Attacker, PhysboxConstants.KillsStat, 1 );

			if ( GameLogicComponent.UseTeams )
			{
				if ( !TeamKills.ContainsKey( info.Attacker.Team ) )
				{
					TeamKills[info.Attacker.Team] = 0;
				}

				TeamKills[info.Attacker.Team]++;
			}

			Scene.RunEvent<IGameEvents>( x => x.OnPlayerScoreUpdate( info.Attacker, info.Attacker.Kills ) );
		}

		info.Victim.RequestSpawn();
	}

	[Rpc.Broadcast]
	void IGameEvents.OnPlayerScoreUpdate( PlayerComponent player, int score )
	{
		if ( RoundOver )
		{
			return;
		}

		if ( !GameLogicComponent.UseTeams )
		{
			// Someone is getting close!
			if ( score == DeathmatchKillsToWin - 1 )
			{
				var chat = ChatManagerComponent.GetChatManager();
				var name = player.IsPlayer ? player.Network.Owner.DisplayName : player.BotName;

				chat.SendMessage( MessageType.System, $"WARNING! {name} is only one kill away from winning!" );
			}
			// We have a winner!
			else if ( score >= DeathmatchKillsToWin )
			{
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
		else
		{
			var team = player.GetComponent<PlayerComponent>().Team;

			// Someone is getting close!
			if ( GetKillsForTeam( team ) == DeathmatchKillsToWin - 1 )
			{
				var chat = ChatManagerComponent.GetChatManager();
				chat.SendMessage( MessageType.System, $"WARNING! Team {team} is only one kill away from winning!" );
			}

			// We have a winner!
			else if ( GetKillsForTeam( team ) >= DeathmatchKillsToWin )
			{
				DeclareWinner( player );
				var chat = ChatManagerComponent.GetChatManager();
				var playerComp = player.GetComponent<PlayerComponent>();
				var name = playerComp.IsPlayer ? playerComp.Network.Owner.DisplayName : playerComp.BotName;

				chat.SendMessage( MessageType.System,
					$"Round over! Team {team} wins with {score} kills! Last kill achieved by {name}!" );
				PhysboxUtilites.IncrementStatForPlayer( playerComp, PhysboxConstants.WinsStat, 1 );

				// Hand control back to master logic component.
				Scene.RunEvent<IGameEvents>( x => x.OnRoundEnd() );
				RoundOver = true;
			}
		}
	}

	[Rpc.Broadcast]
	void IGameEvents.OnRoundStart()
	{
		base.OnRoundStart();
		TeamKills.Clear();

		foreach ( var player in Scene.GetAllComponents<PlayerComponent>() )
		{
			if ( player.SpawnCancellationToken.CanBeCanceled )
			{
				player.SpawnCancellationTokenSource.Cancel();
			}

			player.Kills = 0;
			player.Deaths = 0;

			// Don't spawn a player unless they are on a team.
			if ( GameLogicComponent.UseTeams )
			{
				if ( player.Team != Team.None )
				{
					player.Spawn();
				}
			}
			else
			{
				player.Spawn();
			}
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

	public int GetKillsForTeam( Team team )
	{
		return TeamKills.GetValueOrDefault( team, 0 );
	}
}
