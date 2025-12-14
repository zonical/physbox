using System;
using Sandbox;
using Physbox;
using Sandbox.Diagnostics;

[Hide]
[PhysboxGamemode( GameModes.Dodgeball )]
public class DodgeballGameMode : BaseGameMode, IPhysboxGameEvents
{
	[ConVar( "pb_dodgeball_lives", ConVarFlags.Server )]
	public static int MaxLives { get; set; } = 3;

	[Sync] public NetDictionary<Guid, int> LivesLeft { get; set; } = new();
	[Sync] public NetList<Team> TeamsRemaining { get; set; } = new();

	private Dictionary<Guid, List<GameObject>> Balloons = new();

	[Rpc.Broadcast]
	void IPhysboxGameEvents.OnRoundStart()
	{
		base.OnRoundStart();

		LivesLeft.Clear();
		TeamsRemaining.Clear();

		foreach ( var team in Enum.GetValues<Team>() )
		{
			if ( team == Team.None )
			{
				continue;
			}

			TeamsRemaining.Add( team );
		}

		foreach ( var player in Scene.GetAllComponents<PlayerComponent>() )
		{
			if ( player.SpawnCancellationToken.CanBeCanceled )
			{
				player.SpawnCancellationTokenSource.Cancel();
			}

			LivesLeft.Add( player.Id, MaxLives );

			player.Kills = 0;
			player.Deaths = 0;

			// If we already have balloons, destroy them.
			if ( Balloons.TryGetValue( player.Id, out var balloons ) )
			{
				foreach ( var balloon in balloons )
				{
					balloon?.Destroy();
				}
			}

			Balloons[player.Id] = [];

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
	void IPhysboxGameEvents.OnPlayerDeath( PhysboxDamageInfo info )
	{
		Assert.True( LivesLeft.ContainsKey( info.Victim.Id ) );

		LivesLeft[info.Victim.Id]--;
		if ( LivesLeft[info.Victim.Id] > 0 )
		{
			info.Victim.RequestSpawn();
		}
		else
		{
			var chat = ChatManagerComponent.GetChatManager();
			chat.SendMessage( MessageType.System, $"{info.Victim.Name} has been eliminated!" );
		}

		// Increment leaderboard stats.
		IncrementLeaderboardStats( info.Victim, info.Attacker );

		// Our team has run out of lives, oh no!!! 
		if ( GetLivesRemainingForTeam( info.Victim.Team ) <= 0 )
		{
			TeamsRemaining.Remove( info.Victim.Team );

			var chat = ChatManagerComponent.GetChatManager();
			chat.SendMessage( MessageType.System, $"{info.Victim.Team} has been eliminated!" );
		}

		// If there is only one team remaining, declare them as victorious!
		if ( TeamsRemaining.Count == 1 )
		{
			var winningTeam = TeamsRemaining.First();
			DeclareWinner( winningTeam );

			var chat = ChatManagerComponent.GetChatManager();
			chat.SendMessage( MessageType.System, $"Round over! {winningTeam} wins!" );

			foreach ( var player in Scene.GetAllComponents<PlayerComponent>().Where( x => x.Team == winningTeam ) )
			{
				PhysboxUtilities.IncrementStatForPlayer( player, PhysboxConstants.WinsStat, 1 );
			}

			// Hand control back to master logic component.
			Scene.RunEvent<IPhysboxGameEvents>( x => x.OnRoundEnd() );
			RoundOver = true;
		}
	}


	// Dumb wrapper.
	private PlayerComponent GetPlayerFromId( Guid id )
	{
		return (PlayerComponent)Scene.Directory.FindComponentByGuid( id );
	}

	private void IncrementLeaderboardStats( PlayerComponent victim, PlayerComponent attacker )
	{
		victim.Deaths++;
		if ( victim.Team != attacker?.Team )
		{
			attacker?.Kills += 1;
		}
	}

	public int GetLivesRemainingForTeam( Team team )
	{
		var victimTeamPlayers = LivesLeft.Where( x => GetPlayerFromId( x.Key ).Team == team );
		return victimTeamPlayers.Sum( x => x.Value );
	}

	void IPhysboxGameEvents.OnPlayerSpawn( PlayerComponent player )
	{
		// Safety measure.
		if ( !LivesLeft.ContainsKey( player.Id ) )
		{
			LivesLeft[player.Id] = MaxLives;
		}
	}

	private void CreateBalloonsForPlayer( PlayerComponent player )
	{
		// If we already have balloons, destroy them.
		if ( Balloons.TryGetValue( player.Id, out var balloons ) )
		{
			foreach ( var balloon in balloons )
			{
				balloon?.Destroy();
			}
		}

		Balloons[player.Id] = [];

		// Create balloons based on the amount of lives left.
		for ( var i = 0; i < 3; i++ )
		{
			var balloon = CreateBalloon();
			if ( balloon is null )
			{
				continue;
			}

			Balloons[player.Id].Add( balloon );
			balloon.WorldPosition = player.WorldPosition + new Vector3( 0, 0, 96 );

			var attachmentPoint = player.RendererComponent.GetBoneObject( "head" );
			if ( attachmentPoint is null )
			{
				// Fallback option.
				attachmentPoint =
					player.GameObject.GetAllObjects( true ).FirstOrDefault( x =>
						x.Tags.Contains( PhysboxConstants.BalloonAttachmentTag ) );

				if ( attachmentPoint is null )
				{
					Log.Error( "Could not find attachment point." );
					balloon.Destroy();
					continue;
				}
			}

			var springJoint = balloon.GetComponent<SpringJoint>();
			springJoint.Body = attachmentPoint;

			var rigidbody = balloon.GetComponent<Rigidbody>();
			rigidbody.MassOverride = 1;
			rigidbody.RigidbodyFlags = RigidbodyFlags.DisableCollisionSounds;

			var collider = balloon.GetComponent<SphereCollider>();
			collider.ColliderFlags = ColliderFlags.IgnoreTraces | ColliderFlags.IgnoreMass;

			var line = balloon.GetComponent<LineRenderer>( true );
			line.Enabled = true;
			line.UseVectorPoints = false;
			line.Points = new List<GameObject> { balloon, attachmentPoint };

			var model = balloon.GetComponent<ModelRenderer>();
			model.Tint = PhysboxUtilities.GetTeamColor( player.Team );

			balloon.NetworkSpawn();
		}
	}

	private GameObject CreateBalloon()
	{
		var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/dodgeball/balloon.prefab" );
		if ( prefab is null )
		{
			Log.Error( "Could not find prefab file." );
			return null;
		}

		// Spawn this object and make the client the owner.
		var prefabScene = SceneUtility.GetPrefabScene( prefab );
		var go = prefabScene.Clone( new Transform(), name: "Balloon" );
		go.BreakFromPrefab();

		return go;
	}
}
