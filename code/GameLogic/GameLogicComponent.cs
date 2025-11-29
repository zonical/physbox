using Sandbox;
using System;
using System.Threading.Tasks;

[Group( "Physbox" )]
[Title( "Game Logic Component" )]
[Icon( "directions_run" )]
[Tint( EditorTint.Yellow )]
[Hide]
public partial class GameLogicComponent :
	Component,
	Component.INetworkListener,
	IGameEvents
{
	// The exact time this timer ends ( Time.Now + whatever the gamemode wants.
	// This value is set in each gamemode component.
	[Property]
	[ReadOnly]
	[Sync]
	[ActionGraphIgnore]
	public int TimeShouldEnd { get; set; } = -1;

	[Property]
	[ReadOnly]
	[Sync]
	[Title( "Game Mode" )]
	public BaseGameMode GameModeComponent { get; set; }

	[Property]
	[ReadOnly]
	[Title( "Time Remaining" )]
	[Icon( "alarm" )]
	public int TimeLeft => TimeShouldEnd - (int)Time.Now;

	public bool RoundOver => GameModeComponent?.RoundOver ?? false;

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
		{
			return;
		}

		if ( PhysboxUtilites.IsMainMenuScene() )
		{
			return;
		}

		// Make a lobby if we don't have one by this point.
		if ( !Networking.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";
			Log.Info( "Lobby doesn't exist - creating a new one!" );

			await Task.DelayRealtimeSeconds( 0.1f );
			PhysboxUtilites.CreateNewLobby();
		}
	}


	// Called when a player has joined the lobby. We create a player prefab here and spawn them in.
	// PlayerComponent will fire the OnPlayerReady event once the component is fully initialised.
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
		var go = prefabScene.Clone( new Transform(), name: $"Player - {channel.DisplayName}" );
		go.BreakFromPrefab();
		go.NetworkSpawn( channel );

		// Once the player has been spawned on the network, we can go ahead and
		// initialise them. Doing this in OnNetworkSpawn is too early.
		var player = go.GetComponent<PlayerComponent>();
		player.InitPlayer();
	}

	protected override void OnEnabled()
	{
		// Delete ourselves if we're in the main menu.
		if ( PhysboxUtilites.IsMainMenuScene() )
		{
			DestroyGameObject();
			return;
		}

		Log.Info( "Initialising new Physbox game." );
		GameObject.Name = $"Physbox Game - {GameMode}";

		var mapInfo = Scene.Get<MapInformationComponent>();
		if ( PhysboxUtilites.MapOverridesDefaultSpawnpoints() )
		{
			// If there are any default spawnpoints, get rid of them.
			foreach ( var oldSpawn in Scene.GetAllComponents<SpawnPoint>() )
			{
				oldSpawn.GameObject.Destroy();
			}

			Log.Info( "Updated spawnpoints." );
		}

		// Save the props that the developer has manually placed in the level.
		var system = Scene.GetSystem<PersistentObjectRefreshSystem>();
		system.SaveProps();

		// Start game.
		StartGame();

		// Spawn bots.
		for ( var i = 0; i < MaxBots; i++ )
		{
			var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/bot.prefab" );
			if ( prefab is null )
			{
				Log.Error( "Could not find prefab file." );
				return;
			}

			// Spawn this object and make the client the owner.
			var prefabScene = SceneUtility.GetPrefabScene( prefab );
			var go = prefabScene.Clone( new Transform(), name: "BOT (Placeholder)" );
			go.BreakFromPrefab();
			go.NetworkSpawn();

			// Once the player has been spawned on the network, we can go ahead and
			// initalise them. Doing this in OnNetworkSpawn is too early.
			var player = go.GetComponent<PlayerComponent>();
			player.InitBot();
		}

		Log.Info( $"Added {MaxBots} bots to game." );
	}

	public void SetGameMode( PhysboxConstants.GameModes gameMode )
	{
		GameMode = gameMode;
		RestartGame();
	}

	[Rpc.Host( NetFlags.HostOnly | NetFlags.Reliable | NetFlags.SendImmediate )]
	[ActionGraphIgnore]
	public void StartGame()
	{
		// Create gamemode.
		if ( !Scene.GetAllComponents<BaseGameMode>().Any() )
		{
			switch ( GameMode )
			{
				case PhysboxConstants.GameModes.None:
					GameModeComponent = GameObject.AddComponent<EmptyGameMode>(); break;
				case PhysboxConstants.GameModes.Deathmatch:
					GameModeComponent = GameObject.AddComponent<DeathmatchGameMode>(); break;
				default:
					{
						Log.Error( $"Invalid gamemode selected! ({GameMode})" );
						break;
					}
			}
		}

		Log.Info( $"Created gamemode component - {GameMode.ToString()}" );

		if ( UseTimer )
		{
			TimeShouldEnd = (int)Time.Now + TimerLengthInSeconds;
		}

		// Slight delay to give everything else time to set up.
		Invoke( 0.1f, () =>
		{
			Scene.RunEvent<IGameEvents>( x => x.OnRoundStart() );
		} );
	}

	[Rpc.Broadcast]
	[ActionGraphIgnore]
	private void BroadcastRestartGame()
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

	private void RestartGame()
	{
		BroadcastRestartGame();
		StartGame();
	}

	// Handles timers.
	protected override void OnUpdate()
	{
		if ( !UseTimer )
		{
			return;
		}

		if ( TimeLeft > 0 || TimeShouldEnd == -1 )
		{
			return;
		}

		TimeShouldEnd = -1;

		Scene.RunEvent<IGameEvents>( x => x.OnRoundEnd() );
	}


	[Rpc.Host( NetFlags.HostOnly | NetFlags.Reliable | NetFlags.SendImmediate )]
	[ActionGraphIgnore]
	public void OnRoundEnd()
	{
		//Log.Info( "GameLogicComponent.OnRoundEnd()" );
		TimeShouldEnd = -1;

		Invoke( RoundIntermissionSeconds, RestartGame );
	}
}
