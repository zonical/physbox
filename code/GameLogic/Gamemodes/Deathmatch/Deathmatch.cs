using Sandbox;
using System;

[Hide]
[PhysboxGamemode( GameModes.Deathmatch )]
public partial class DeathmatchGameMode : BaseGameMode, IPhysboxGameEvents, IPhysboxNetworkEvents
{
	[ConVar( "pb_deathmatch_kills_to_win", ConVarFlags.Server )]
	public static int DeathmatchKillsToWin { get; set; } = 5;

	[Sync] public NetDictionary<Team, int> TeamKills { get; set; } = new();

	void IPhysboxGameEvents.OnPlayerDeath( PhysboxDamageInfo info )
	{
		if ( Networking.IsHost || RoundOver || info.Victim is null )
		{
			if ( RoundOver || info.Victim is null )
			{
				return;
			}

			Log.Info( $"{info.Victim?.Name} died to {info.Attacker?.Name}, caused by {info.Prop}" );
			if ( info.Attacker is not null && !info.IsSuicide )
			{
				if ( GameLogicComponent.UseTeams &&
				     info.Victim.Team == info.Attacker.Team )
				{
					return;
				}

				// Add kills to attacking player.
				info.Attacker.Kills++;
				PhysboxUtilities.IncrementStatForPlayer( info.Attacker, PhysboxConstants.KillsStat, 1 );

				// Add kills to attacking player's team.
				if ( GameLogicComponent.UseTeams )
				{
					TeamKills[info.Attacker.Team]++;
				}

				Scene.RunEvent<IPhysboxGameEvents>( x => x.OnPlayerScoreUpdate( info.Attacker, info.Attacker.Kills ) );
			}

			info.Victim?.Deaths += 1;
			PhysboxUtilities.IncrementStatForPlayer( info.Victim, PhysboxConstants.DeathsStat, 1 );
		}

		info.Victim?.RequestSpawn();
	}

	void IPhysboxGameEvents.OnPlayerScoreUpdate( PlayerComponent player, int score )
	{
		if ( !Networking.IsHost || RoundOver )
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
				PhysboxUtilities.IncrementStatForPlayer( playerComp, PhysboxConstants.WinsStat, 1 );

				// Hand control back to master logic component.
				Scene.RunEvent<IPhysboxGameEvents>( x => x.OnRoundEnd() );
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
				DeclareWinner( team );
				var chat = ChatManagerComponent.GetChatManager();
				var playerComp = player.GetComponent<PlayerComponent>();
				var name = playerComp.IsPlayer ? playerComp.Network.Owner.DisplayName : playerComp.BotName;

				chat.SendMessage( MessageType.System,
					$"Round over! Team {team} wins with {score} kills! Last kill achieved by {name}!" );
				PhysboxUtilities.IncrementStatForPlayer( playerComp, PhysboxConstants.WinsStat, 1 );

				// Hand control back to master logic component.
				Scene.RunEvent<IPhysboxGameEvents>( x => x.OnRoundEnd() );
				RoundOver = true;
			}
		}
	}

	void IPhysboxGameEvents.OnRoundStart()
	{
		base.OnRoundStart();

		// Reset team kills.
		TeamKills.Clear();
		if ( GameLogicComponent.UseTeams )
		{
			foreach ( var team in Game.AvaliableTeams )
			{
				TeamKills.Add( team, 0 );
			}
		}

		foreach ( var player in Scene.GetAllComponents<PlayerComponent>() )
		{
			if ( Networking.IsHost )
			{
				if ( player.SpawnCancellationToken.CanBeCanceled )
				{
					player.SpawnCancellationTokenSource.Cancel();
				}
			}

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

	void IPhysboxGameEvents.OnRoundEnd()
	{
		base.OnRoundEnd();

		if ( !Networking.IsHost )
		{
			return;
		}

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
