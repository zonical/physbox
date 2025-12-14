using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Sandbox;
using Sandbox.Diagnostics;
using Networking = Sandbox.Debug.Networking;

public partial class GameLogicComponent
{
	[Property] [Feature( "Prefabs" )] private GameObject PlayerPrefab { get; set; }
	[Property] [Feature( "Prefabs" )] private GameObject BotPrefab { get; set; }

	/// <summary>
	/// All the player components created within the scene.
	/// </summary>
	private Dictionary<Guid, PlayerComponent> _players = new();

	public ReadOnlyDictionary<Guid, PlayerComponent> Players => _players.AsReadOnly();

	/// <summary>
	/// Called when a player has connected to the server.
	/// </summary>
	/// <param name="channel">Connection.</param>
	void IPhysboxNetworkEvents.OnPlayerConnected( Connection channel )
	{
		Log.Info( $"GameLogicComponent::OnPlayerConnected({channel.DisplayName})" );

		// Create a player. This won't actually make the player run around
		// the world just yet. We'll get there soon.
		CreatePlayer( channel );
	}

	/// <summary>
	/// Creates a player and spawns them into the world.
	/// </summary>
	/// <param name="channel">Connection that will own this player.</param>
	private void CreatePlayer( Connection channel )
	{
		Log.Info( $"GameLogicComponent::CreatePlayer({channel.DisplayName})" );

		Assert.NotNull( PlayerPrefab );

		// Create player.
		var player = PlayerPrefab.Clone();
		player.Name = $"Player - {channel.DisplayName}";
		var comp = player.GetComponent<PlayerComponent>();

		player.NetworkSpawn( channel );
		_players.Add( channel.Id, comp );
	}

	/// <summary>
	/// Called when a bot has been added to the server.
	/// </summary>
	/// <param name="botId">Connection that will own this bot.</param>
	void IPhysboxNetworkEvents.OnBotConnected( Guid botId )
	{
		// Create a bot. This won't actually make the bot run around
		// the world just yet. We'll get there soon.
		CreateBot( botId );
	}

	/// <summary>
	/// Creates a bot and spawns them into the world.
	/// </summary>
	private void CreateBot( Guid botId )
	{
		Assert.NotNull( BotPrefab );
		var bot = BotPrefab.Clone();

		var player = bot.GetComponent<PlayerComponent>();
		_players.Add( botId, player );
		player.IsBot = true; // Sanity.

		bot.NetworkSpawn();
	}

	/// <summary>
	/// Called when a client has disconnected from the server.
	/// </summary>
	/// <param name="channel"></param>
	void IPhysboxNetworkEvents.OnPlayerDisconnected( Connection channel )
	{
		var player = _players[channel.Id];
		_players.Remove( channel.Id );

		// Destroy the GameObject (if it hasn't been already).
		if ( !player.GameObject?.IsDestroyed ?? false )
		{
			player.GameObject.Destroy();
		}

		// If we were holding a prop, destroy that too.
		if ( !player.HeldGameObject?.IsDestroyed ?? false )
		{
			player.HeldGameObject.Destroy();
		}
	}

	/// <summary>
	/// Called when the server enters hibernation.
	/// </summary>
	void IPhysboxNetworkEvents.OnServerEnterHibernation()
	{
		// TODO: Stop game.
		_players.Clear();
	}

	/// <summary>
	/// Called when the server exits hibernation.
	/// </summary>
	void IPhysboxNetworkEvents.OnServerExitHibernation()
	{
		// Create our bots.
		for ( var i = 0; i < MaxBots; i++ )
		{
			Scene.RunEvent<IPhysboxNetworkEvents>( x => x.OnBotConnected( Guid.NewGuid() ) );
		}
	}
}

public partial class PhysboxUtilities
{
	/// <summary>
	/// Gets all connected players.
	/// </summary>
	/// <returns></returns>
	public ImmutableList<PlayerComponent> GetPlayers()
	{
		return GameLogicComponent.GetGameInstance().Players.Values?.ToImmutableList();
	}

	/// <summary>
	/// Gets all connected players.
	/// </summary>
	/// <returns></returns>
	public ImmutableList<PlayerComponent> GetPlayersByTeam( Team team )
	{
		return GameLogicComponent.GetGameInstance().Players.Values?.Where( x => x.Team == team ).ToImmutableList();
	}
}
