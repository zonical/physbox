using Sandbox;
using System;
using System.Threading.Tasks;
using Sandbox.Audio;

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

	private readonly Dictionary<int, string> AnnouncerSecondsSounds = new()
	{
		{ 60, "sounds/announcer/60-seconds.sound" },
		{ 30, "sounds/announcer/30-seconds.sound" },
		{ 10, "sounds/announcer/10-seconds.sound" },
		{ 5, "sounds/announcer/countdown-5.sound" },
		{ 4, "sounds/announcer/countdown-4.sound" },
		{ 3, "sounds/announcer/countdown-3.sound" },
		{ 2, "sounds/announcer/countdown-2.sound" },
		{ 1, "sounds/announcer/countdown-1.sound" }
	};

	private List<int> AnnouncerSoundsAlreadyPlayed = new();

	public bool RoundOver => GameModeComponent?.RoundOver ?? false;

	/// <summary>
	/// Creates a lobby if we don't already have one by this point.
	/// </summary>
	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor || PhysboxUtilites.IsMainMenuScene() )
		{
			return;
		}

		if ( !Networking.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";
			Log.Info( "Lobby doesn't exist - creating a new one!" );

			await Task.DelayRealtimeSeconds( 0.1f );
			PhysboxUtilites.CreateNewLobby();
		}
	}

	/// <summary>
	/// Called when a player has joined the lobby. We create a player prefab here and spawn them in.
	/// PlayerComponent will fire the OnPlayerReady event once the component is fully initialised.
	/// </summary>
	/// <param name="channel">Incoming connection.</param>
	[ActionGraphIgnore]
	public void OnActive( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' has joined the game" );
		CreatePlayer( channel );
	}

	protected override void OnEnabled()
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		// Delete ourselves if we're in the main menu.
		if ( PhysboxUtilites.IsMainMenuScene() )
		{
			DestroyGameObject();
			return;
		}

		Log.Info( "Initialising new Physbox game." );
		GameObject.Name = $"Physbox Game - {GameMode}";

		SpawnpointOverrideCheck();
		SaveProps();
		CreateBots();

		// Slight delay to give everything else time to set up.
		Invoke( 3.0f, StartGame );
	}

	/// <summary>
	/// Creates a player and spawns them into the world.
	/// </summary>
	/// <param name="channel">Connection that will own this player.</param>
	private void CreatePlayer( Connection channel )
	{
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

	/// <summary>
	/// Creates a number of bots (pb_maxbots) and spawns them into the world.
	/// </summary>
	private void CreateBots()
	{
		// Spawn bots.
		var lastTeam = Team.None;

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

			player.Team = lastTeam + 1;
			lastTeam = player.Team;

			if ( lastTeam == Enum.GetValues<Team>().Max() )
			{
				lastTeam = Team.None;
			}

			player.IsBot = true; // Sanity.
			player.InitBot();
		}

		Log.Info( $"Added {MaxBots} bots to game." );
	}

	/// <summary>
	/// Tells PersistentObjectRefreshSystem to save props before the game begins.
	/// </summary>
	private void SaveProps()
	{
		// Save the props that the developer has manually placed in the level.
		var system = Scene.GetSystem<PersistentObjectRefreshSystem>();
		system.SaveProps();
	}

	/// <summary>
	/// Removes Sandbox spawnpoints if we don't want to keep them around (see MapInformationComponent).
	/// </summary>
	private void SpawnpointOverrideCheck()
	{
		var mapInfo = Scene.Get<MapInformationComponent>();
		if ( !PhysboxUtilites.MapOverridesDefaultSpawnpoints() )
		{
			return;
		}

		// If there are any default spawnpoints, get rid of them.
		foreach ( var oldSpawn in Scene.GetAllComponents<SpawnPoint>() )
		{
			oldSpawn.GameObject.Destroy();
		}

		Log.Info( "Updated spawnpoints." );
	}

	/// <summary>
	/// Sets the current gamemode.
	/// </summary>
	/// <param name="gameMode"></param>
	public void SetGameMode( PhysboxConstants.GameModes gameMode )
	{
		GameMode = gameMode;
		RestartGame();
	}

	/// <summary>
	/// Creates the gamemode component, adjusts the timer (if we have it enabled),
	/// and calls round start.
	/// </summary>
	[Rpc.Host( NetFlags.HostOnly | NetFlags.Reliable | NetFlags.SendImmediate )]
	[ActionGraphIgnore]
	public void StartGame()
	{
		// Create gamemode.
		if ( !Scene.GetAllComponents<BaseGameMode>().Any() )
		{
			CreateGamemodeComponent();
		}

		if ( UseTimer )
		{
			TimeShouldEnd = (int)Time.Now + TimerLengthInSeconds;
		}

		Scene.RunEvent<IGameEvents>( x => x.OnRoundStart() );
	}

	/// <summary>
	/// Creates a component based on the gamemode.
	/// </summary>
	private void CreateGamemodeComponent()
	{
		var gameModes = TypeLibrary.GetTypes<BaseGameMode>();
		var gamemode =
			gameModes.FirstOrDefault( x => x.GetAttribute<PhysboxGamemodeAttribute>()?.GameMode == GameMode, null );

		if ( gamemode is null )
		{
			Log.Error( $"Invalid gamemode selected! ({GameMode})" );
			return;
		}

		// BaseGameMode is derived from Component.
		GameModeComponent = (BaseGameMode)Components.Create( gamemode );
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

		AnnouncerSoundsAlreadyPlayed.Clear();
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

	protected override void OnFixedUpdate()
	{
		if ( !UseTimer || TimeLeft < 0 || TimeShouldEnd == -1 )
		{
			return;
		}

		// Play announcer sound.
		if ( AnnouncerSecondsSounds.TryGetValue( TimeLeft, out var sound ) &&
		     !AnnouncerSoundsAlreadyPlayed.Contains( TimeLeft ) )
		{
			AnnouncerSoundsAlreadyPlayed.Add( TimeLeft );
			Sound.Play( sound, Mixer.FindMixerByName( "UI" ) );
		}
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
