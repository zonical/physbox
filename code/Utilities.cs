using Physbox;
using Sandbox.Diagnostics;
using Sandbox.Network;
using Sandbox.Services;
using Sandbox.UI;

public partial class PhysboxUtilities
{
	/// <summary>
	/// Creates a prop in the world from a prop definition resource.
	/// </summary>
	/// <param name="resource">Resoure of the prop.</param>
	/// <param name="transform">Transform of the prop (optional).</param>
	/// <returns></returns>
	[ActionGraphNode( "physbox.create_prop" )]
	[Title( "Create Prop" )]
	[Group( "Physbox" )]
	[Icon( "inventory" )]
	public static GameObject CreatePropFromResource( PropDefinitionResource resource, Transform transform = default )
	{
		Assert.IsValid( resource );

		//Log.Info( $"Creating prop {resource.ResourcePath}" );
		var GameObject = new GameObject( true, "Breakable Prop" );
		Assert.IsValid( GameObject );

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
		GameObject.WorldTransform = transform;

		GameObject.Network.AlwaysTransmit = false;
		GameObject.NetworkMode = NetworkMode.Object;
		GameObject.Network.SetOwnerTransfer( OwnerTransfer.Takeover );
		GameObject.NetworkSpawn( Connection.Host );

		return GameObject;
	}

	/// <summary>
	/// Checks to see whether the active scene is the main menu scene.
	/// </summary>
	/// <returns></returns>
	public static bool IsMainMenuScene()
	{
		Assert.IsValid( Game.ActiveScene );
		var scene = Game.ActiveScene;
		return scene.Get<MainMenu>() is not null;
	}

	/// <summary>
	/// Checks to see whether the scene is the main menu scene.
	/// </summary>
	/// <param name="scene">Scene to check.</param>
	/// <returns></returns>
	public static bool IsMainMenuScene( Scene scene )
	{
		Assert.IsValid( scene );
		return scene.Get<MainMenu>() is not null;
	}

	public static string GetCurrentMapName()
	{
		Assert.IsValid( Game.ActiveScene );

		// The proper map name and author should be stored in a SceneInformation component.
		var sceneInformation = Game.ActiveScene.Get<SceneInformation>();
		return sceneInformation is not null
			? $"{sceneInformation.Title} (by {sceneInformation.Author})"
			: Networking.MapName; // Last ditch effort.
	}

	public static string GetGameModeIcon( GameModes gameMode )
	{
		return gameMode.GetAttributeOfType<IconAttribute>()?.Value ?? "❓";
	}

	public static bool MapOverridesDefaultSpawnpoints()
	{
		Assert.IsValid( Game.ActiveScene );
		var mapInfo = Game.ActiveScene.Get<MapInformationComponent>();
		return mapInfo?.OverrideDefaultSpawnpoints ?? false;
	}

	public static ChatManagerComponent GetChatComponent()
	{
		Assert.IsValid( Game.ActiveScene );
		return Game.ActiveScene.Get<ChatManagerComponent>();
	}

	public static Color GetTeamColor( Team team )
	{
		return team.GetAttributeOfType<TeamColorAttribute>()?.Color ?? Color.White;
	}

	/// <summary>
	/// Used in UI image elements where player avatars are displayed. Players use
	/// their Steam profile avatar while bots use an image from disk.
	/// </summary>
	/// <param name="player">Source for the image.</param>
	/// <returns>A string intended for the src parameter for an img element.</returns>
	public static string GetSourceForPlayerIcon( PlayerComponent player )
	{
		Assert.IsValid( player );
		return player.IsPlayer ? $"avatar:{player.Network.Owner.SteamId}" : "materials/ui/hud_chat_system_icon.png";
	}

	/// <summary>
	/// Sends a chat message that will only be visible to the local connection.
	/// </summary>
	/// <param name="type">Message type (player, system, etc...)</param>
	/// <param name="message">Text of the message.</param>
	public static void SendLocalChatMessage( MessageType type, string message )
	{
		var chat = GetChatComponent();
		if ( chat is null )
		{
			return;
		}

		using ( Rpc.FilterInclude( c => c.Id == Connection.Local.Id ) )
		{
			chat.SendMessage( type, message );
		}
	}

	/// <summary>
	/// Sends a chat message that will only be visible to one connection.
	/// </summary>
	/// <param name="player">Connection to send the message to.</param>
	/// <param name="type">Message type (player, system, etc...)</param>
	/// <param name="message">Text of the message.</param>
	public static void SendMessageToOnlyConnection( Connection player, MessageType type, string message )
	{
		var chat = GetChatComponent();
		if ( chat is null )
		{
			return;
		}

		using ( Rpc.FilterInclude( c => c.Id == player.Id ) )
		{
			chat.SendMessage( type, message );
		}
	}

	public static void CreateNewLobby(
		int maxPlayers = 64,
		string lobbyName = "Physbox Deathmatch Lobby",
		GameModes gameMode = GameModes.Deathmatch,
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
	private static void IncrementStatRPC( string stat, int value )
	{
		Stats.Increment( stat, value );
	}

	[Rpc.Broadcast]
	private static void TriggerAchievementRPC( string achievement )
	{
		Achievements.Unlock( achievement );
	}

	/// <summary>
	///     Increments a stat on the s&box backend. We use this as a convenient wrapper to ensure that
	///     stats are going to the right connections. If I was a good networking programmer, I wouldn't
	///     have to worry about this, but I want to be safe.
	/// </summary>
	/// <param name="player">Player to increment stat for (non bot).</param>
	/// <param name="stat">Stat to increment.</param>
	/// <param name="value">Value to increment by.</param>
	public static void IncrementStatForPlayer( PlayerComponent player, string stat, int value )
	{
		Assert.IsValid( player );
		if ( player.IsBot )
		{
			return;
		}

		using ( Rpc.FilterInclude( c => c.Id == player.Network.Owner.Id ) )
		{
			IncrementStatRPC( stat, value );
		}
	}

	/// <summary>
	///     Triggers an achievement on the s&box backend. We use this as a convenient wrapper to ensure that
	///     stats are going to the right connections. If I was a good networking programmer, I wouldn't
	///     have to worry about this, but I want to be safe.
	/// </summary>
	/// <param name="player">Player to increment stat for (non bot).</param>
	/// <param name="achievement">Achievement to trigger.</param>
	public static void TriggerAchievementForPlayer( PlayerComponent player, string achievement )
	{
		Assert.IsValid( player );
		if ( player.IsBot )
		{
			return;
		}

		using ( Rpc.FilterInclude( c => c.Id == player.Network.Owner.Id ) )
		{
			TriggerAchievementRPC( achievement );
		}
	}
}
