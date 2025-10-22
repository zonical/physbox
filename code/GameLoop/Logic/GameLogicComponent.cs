using Sandbox;
using Sandbox.Network;
using System;
using System.Threading.Tasks;

[Hide]
public partial class GameLogicComponent :
	Component,
	Component.INetworkListener,
	IGameEvents
{
	#region Properties
	// The exact time this timer ends ( Time.Now + whatever the gamemode wants.
	// This value is set in each gamemode component.
	[Property, ReadOnly, Sync, ActionGraphIgnore]
	public int TimeShouldEnd { get; set; } = -1;

	[Property, ReadOnly, Sync, Title( "Game Mode" )]
	public BaseGameMode GameModeComponent { get; set; }

	[Property, ReadOnly, Title( "Time Remaining" ), Icon( "alarm" )]
	public int TimeLeft => TimeShouldEnd - (int)Time.Now;
	public bool RoundOver => GameModeComponent.RoundOver;

	#endregion

	#region Networking
	protected override void OnEnabled()
	{
		if ( !Networking.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";
			var config = new LobbyConfig();
			Networking.CreateLobby( config );
		}

		// If there are any default spawnpoints, convert them to ours.
		foreach ( var oldSpawn in Scene.GetAllComponents<SpawnPoint>() )
		{
			var go = new GameObject( true, "Spawnpoint (Physbox)" );
			var comp = go.AddComponent<PhysboxSpawnpoint>();
			comp.AnyGameMode = true;
			go.WorldPosition = oldSpawn.WorldPosition;
			go.WorldRotation = oldSpawn.WorldRotation;

			oldSpawn.GameObject.Destroy();
		}

		// Start game.
		StartGame();
	}


	// Called when a player has joined the lobby. We create a player prefab here and spawn them in.
	// PlayerComponent will fire the OnPlayerReady event once the component is fully initalised.
	[ActionGraphIgnore]
	public void OnActive( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' has joined the game" );

		var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/player.prefab" );
		if ( prefab is null )
		{
			Log.Error( "Could not find prefab file." );
			return;
		}

		// Spawn this object and make the client the owner.
		var prefabScene = SceneUtility.GetPrefabScene( prefab );
		var go = prefabScene.Clone( new(), name: $"Player - {channel.DisplayName}" );
		go.BreakFromPrefab();
		go.NetworkSpawn( channel );

		// Once the player has been spawned on the network, we can go ahead and
		// initalise them. Doing this in OnNetworkSpawn is too early.
		var player = go.GetComponent<PlayerComponent>();
		player.InitPlayer();
	}

	#endregion

	#region Game Logic
	public void SetGameMode( PhysboxConstants.GameModes gameMode )
	{
		GameMode = gameMode;
		RestartGame();
	}

	[Rpc.Host( NetFlags.HostOnly | NetFlags.Reliable | NetFlags.SendImmediate ), ActionGraphIgnore]
	public void StartGame()
	{
		// Create gamemode.
		if ( !Scene.GetAllComponents<BaseGameMode>().Any() )
		{
			switch ( GameMode )
			{
				case PhysboxConstants.GameModes.None: GameModeComponent = GameObject.AddComponent<EmptyGameMode>(); break;
				case PhysboxConstants.GameModes.Deathmatch: GameModeComponent = GameObject.AddComponent<DeathmatchGameMode>(); break;
				default:
					{
						Log.Error( $"Invalid gamemode selected! ({GameMode})" );
						break;
					}
			}
		}

		if ( UseTimer )
		{
			TimeShouldEnd = (int)Time.Now + TimerLengthInSeconds;
		}

		// Slight delay to give everything else time to setup.
		Invoke( 0.1f, () =>
		{
			Scene.RunEvent<IGameEvents>( x => x.OnRoundStart() );
		} );
	}

	[Rpc.Broadcast, ActionGraphIgnore]
	public void BroadcastRestartGame()
	{
		// Destroy all props.
		foreach ( var prop in Scene.GetAllComponents<PropLifeComponent>() )
		{
			prop.GameObject.Destroy();
		}

		// Destroy all breakable meshes.
		foreach ( var mesh in Scene.GetAllComponents<WorldLifeComponent>() )
		{
			mesh.GameObject.Destroy();
		}

		// Reset prop spawn timer.
		Scene.GetSystem<PropSpawnerSystem>().SpawnDelay = 0;
		Scene.GetSystem<PropSpawnerSystem>().CheckDelay = 0;
	}

	public void RestartGame()
	{
		BroadcastRestartGame();
		StartGame();
	}

	[Rpc.Host( NetFlags.HostOnly | NetFlags.Reliable | NetFlags.SendImmediate ), ActionGraphIgnore]
	public void OnRoundEnd()
	{
		//Log.Info( "GameLogicComponent.OnRoundEnd()" );
		TimeShouldEnd = -1;

		Invoke( RoundIntermissionSeconds, RestartGame );
	}

	// Handles timers.
	protected override void OnUpdate()
	{
		if ( !UseTimer ) return;
		//Log.Info( TimeLeft );

		if ( TimeLeft <= 0 && TimeShouldEnd != -1 )
		{
			TimeShouldEnd = -1;

			Scene.RunEvent<IGameEvents>( x => x.OnRoundEnd() );
		}
	}
	#endregion
}
