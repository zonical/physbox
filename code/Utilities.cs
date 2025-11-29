using Physbox;
using Sandbox.Network;
using Sandbox.Services;

public static class PhysboxUtilites
{
	public static GameObject CreatePropFromResource( PropDefinitionResource resource )
	{
		//Log.Info( $"Creating prop {resource.ResourcePath}" );
		var GameObject = new GameObject( true, "Breakable Prop" );
		GameObject.Tags.Add( PhysboxConstants.BreakablePropTag );

		// Create the necessary components.
		var modelRenderer = GameObject.AddComponent<ModelRenderer>();
		var modelCollider = GameObject.AddComponent<ModelCollider>();
		var rigidBody = GameObject.AddComponent<Rigidbody>();
		var life = GameObject.AddComponent<PropLifeComponent>();
		GameObject.AddComponent<ObjectCollisionListenerComponent>();

		// Set our resource (this gets converted to PropDefinitionResource on the game side).
		var defComp = GameObject.AddComponent<PropDefinitionComponent>();
		defComp.Definition = resource;

		// Apply models.
		var model = resource.Model;
		modelRenderer.Model = model;
		modelCollider.Model = model;

		// Apply our mass.
		var mass = resource.Mass;
		rigidBody.MassOverride = mass;

		// Apply our health.
		var maxHealth = resource.MaxHealth;
		life.MaxHealth = maxHealth;
		life.Health = maxHealth;

		// Update our name.
		GameObject.Name = $"Prop ({resource.ResourcePath})";

		if ( !IsMainMenuScene() )
		{
			GameObject.NetworkMode = NetworkMode.Object;
			GameObject.Network.SetOwnerTransfer( OwnerTransfer.Takeover );
			GameObject.NetworkSpawn();
		}

		return GameObject;
	}

	public static bool IsMainMenuScene()
	{
		var scene = Game.ActiveScene;
		return scene.Get<MainMenu>() is not null;
	}

	public static bool IsMainMenuScene( Scene scene )
	{
		return scene.Get<MainMenu>() is not null;
	}

	public static string GetCurrentMapName()
	{
		// The proper map name and author should be stored in a SceneInformation component.
		var sceneInformation = Game.ActiveScene.Get<SceneInformation>();
		if ( sceneInformation is not null )
		{
			return $"{sceneInformation.Title} (by {sceneInformation.Author})";
		}

		// Last ditch effort.
		return Networking.MapName;
	}

	public static string GetGameModeIcon( PhysboxConstants.GameModes gameMode )
	{
		return gameMode.GetAttributeOfType<IconAttribute>()?.Value ?? "❓";
	}

	public static bool MapOverridesDefaultSpawnpoints()
	{
		var mapInfo = Game.ActiveScene.Get<MapInformationComponent>();
		return mapInfo?.OverrideDefaultSpawnpoints ?? false;
	}

	public static void CreateNewLobby(
		int maxPlayers = 64,
		string lobbyName = "Physbox Deathmatch Lobby",
		PhysboxConstants.GameModes gameMode = PhysboxConstants.GameModes.Deathmatch,
		LobbyPrivacy privacy = LobbyPrivacy.Private,
		bool force = false )
	{
		// Disconnect from a lobby if one already exists in this game instance.
		if ( Networking.IsActive )
		{
			if ( force )
			{
				Log.Warning( "Lobby already exists. Disconnecting!" );
				Networking.Disconnect();
			}
			else
			{
				return;
			}
		}

		// Create new lobby.
		LoadingScreen.Title = "Creating Lobby";
		var config = new LobbyConfig { MaxPlayers = maxPlayers, Name = lobbyName, Privacy = privacy };
		Networking.CreateLobby( config );
		Networking.SetData( "gamemode", gameMode.GetAttributeOfType<IconAttribute>().Value ?? "❓" );

		GameLogicComponent.GameMode = gameMode;
	}

	[Rpc.Broadcast]
	public static void IncrementStatRPC( string stat, int value )
	{
		Stats.Increment( stat, value );
	}

	/// <summary>
	///     Increments a stat on the s&box backend. We use this as a convenient wrapper to ensure that
	///     stats are going to the right connections. If I was a good networking programmer, I wouldn't
	///     have to worry about this, but I want to be safe.
	/// </summary>
	/// <param name="player"></param>
	/// <param name="stat"></param>
	/// <param name="value"></param>
	public static void IncrementStatForPlayer( PlayerComponent player, string stat, int value )
	{
		if ( player.IsBot )
		{
			return;
		}

		using ( Rpc.FilterInclude( c => c.Id == player.Network.Owner.Id ) )
		{
			IncrementStatRPC( stat, value );
		}
	}
}
