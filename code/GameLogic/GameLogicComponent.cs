using Sandbox;
using System;

[Group( "Physbox" )]
[Title( "Game Logic Component" )]
[Icon( "directions_run" )]
[Tint( EditorTint.Yellow )]
[Hide]
public partial class GameLogicComponent :
	Component,
	Component.INetworkListener,
	ISceneLoadingEvents,
	IPhysboxGameEvents,
	IPhysboxNetworkEvents
{
	[Property]
	[ReadOnly]
	[Sync]
	[Title( "Game Mode" )]
	public BaseGameMode GameModeComponent { get; set; }

	public List<Team> AvaliableTeams = new();

	public bool RoundOver => GameModeComponent?.RoundOver ?? false;
	public bool GamemodeChangeRequested = false;

	/// <summary>
	/// Starts the game. This is called after OnEnabled.
	/// </summary>
	/// <param name="scene"></param>
	void ISceneLoadingEvents.AfterLoad( Scene scene )
	{
		Log.Info( $"Initialising new Physbox game - {GameMode}" );
		GameObject.Name = $"Physbox Game - {GameMode}";

		if ( Networking.IsHost )
		{
			DetermineAvailableTeams();
		}
	}

	private void DetermineAvailableTeams()
	{
		var physboxSpawns = Scene.GetAllComponents<PhysboxSpawnpoint>();
		if ( physboxSpawns.Any() )
		{
			foreach ( var spawn in physboxSpawns )
			{
				// If there is a spawnpoint that allows all teams,
				// just let them all through! Fuck it!
				if ( spawn.AnyTeam )
				{
					AvaliableTeams = Enum.GetValues<Team>().ToList();
					AvaliableTeams.Remove( Team.None );
					return;
				}

				if ( !AvaliableTeams.Contains( spawn.Team ) )
				{
					AvaliableTeams.Add( spawn.Team );
				}
			}
		}
		// If we have no custom spawnpoints, just allow all teams.
		else
		{
			AvaliableTeams = Enum.GetValues<Team>().ToList();
			AvaliableTeams.Remove( Team.None );
		}

		Log.Info( $"GameLogicComponent - available teams: {string.Join( ",", AvaliableTeams )}" );
	}

	protected override void OnEnabled()
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		// Delete ourselves if we're in the main menu.
		if ( PhysboxUtilities.IsMainMenuScene() )
		{
			DestroyGameObject();
			return;
		}

		SpawnpointOverrideCheck();
		SaveProps();
	}

	/// <summary>
	/// Tells PersistentObjectRefreshSystem to save props before the game begins.
	/// </summary>
	private void SaveProps()
	{
		if ( GameMode == GameModes.Tutorial )
		{
			return;
		}

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
		if ( !PhysboxUtilities.MapOverridesDefaultSpawnpoints() )
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
	public void SetGameMode( GameModes gameMode )
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		GameMode = gameMode;

		// End the current round.
		GamemodeChangeRequested = true;
		Scene.RunEvent<IPhysboxGameEvents>( x => x.OnRoundEnd() );
	}

	/// <summary>
	/// Creates the gamemode component, adjusts the timer (if we have it enabled),
	/// and calls round start.
	/// </summary>
	[Rpc.Broadcast]
	public void StartGame()
	{
		Log.Info( "GameLogicComponent::StartGame() has been called." );

		ForceCleanup();
		PerformGamemodeCheck();
		Scene.RunEvent<IPhysboxGameEvents>( x => x.OnRoundStart() );
	}

	private void PerformGamemodeCheck()
	{
		if ( GamemodeChangeRequested )
		{
			// Get rid of the previous gamemode component (if one exists).
			Scene.Get<BaseGameMode>()?.Destroy();
		}

		// Create gamemode.
		if ( !Scene.GetAllComponents<BaseGameMode>().Any() )
		{
			CreateGamemodeComponent();
			GameObject.Name = $"Physbox Game - {GameMode}";
		}

		Network.Refresh();
	}

	/// <summary>
	/// Creates a component based on the gamemode.
	/// </summary>
	private void CreateGamemodeComponent()
	{
		switch ( GameMode )
		{
			case GameModes.None: GameModeComponent = Components.Create<EmptyGameMode>(); break;
			case GameModes.Deathmatch: GameModeComponent = Components.Create<DeathmatchGameMode>(); break;
			case GameModes.Dodgeball: GameModeComponent = Components.Create<DodgeballGameMode>(); break;
			case GameModes.Tutorial: GameModeComponent = Components.Create<TutorialGameMode>(); break;
			default:
				{
					Log.Error( $"Invalid gamemode selected! ({GameMode})" );
					GameMode = GameModes.Deathmatch;
					GameModeComponent = Components.Create<DeathmatchGameMode>();
					break;
				}
		}
	}

	private void ForceCleanup()
	{
		if ( GameMode != GameModes.Tutorial )
		{
			var props = Scene.GetAllComponents<PropLifeComponent>();
			var meshes = Scene.GetAllComponents<WorldLifeComponent>();

			Log.Info(
				$"GameLogicComponent::ForceCleanup() - cleaning up {props.Count()} props and {meshes.Count()} meshes." );

			// Destroy all props.
			foreach ( var prop in props )
			{
				prop.GameObject.Destroy();
			}

			// Destroy all breakable meshes.
			foreach ( var mesh in meshes )
			{
				mesh.GameObject.Destroy();
			}

			// Reset prop spawn timer.
			Scene.GetSystem<PropSpawnerSystem>().SpawnDelay = 0;
			Scene.GetSystem<PropSpawnerSystem>().CheckDelay = 0;
		}

		AnnouncerSoundsAlreadyPlayed.Clear();
	}

	// Handles timers.
	protected override void OnUpdate()
	{
		if ( !UseTimer || TimeLeft > 0 || TimeShouldEnd == -1 )
		{
			return;
		}

		TimeShouldEnd = -1;
		Scene.RunEvent<IPhysboxGameEvents>( x => x.OnRoundEnd() );
	}

	protected override void OnFixedUpdate()
	{
		if ( !UseTimer || TimeLeft < 0 || TimeShouldEnd == -1 )
		{
			return;
		}

		AnnouncerUpdate();
	}
}
