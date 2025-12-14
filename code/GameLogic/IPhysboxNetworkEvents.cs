using System;

public delegate void OnPlayerConnected( Connection channel );

public delegate void OnPlayerDisconnected( Connection channel );

public delegate void OnBotConnected( Connection channel );

public delegate void OnServerEnterHibernation();

public delegate void OnServerExitHibernation();

public interface IPhysboxNetworkEvents : ISceneEvent<IPhysboxNetworkEvents>
{
	/// <summary>
	/// Called when a client has connected to the server.
	/// </summary>
	/// <param name="channel"></param>
	void OnPlayerConnected( Connection channel ) { }

	/// <summary>
	/// Called when a client has disconnected from the server.
	/// </summary>
	/// <param name="channel"></param>
	void OnPlayerDisconnected( Connection channel ) { }

	/// <summary>
	/// Called when a player has been fully created and is ready to be used by the game.
	/// </summary>
	/// <param name="player"></param>
	void OnPlayerInitialised( PlayerComponent player ) { }

	/// <summary>
	/// Called when a bot has been added to the server.
	/// </summary>
	/// <param name="channel"></param>
	void OnBotConnected( Guid botId ) { }

	/// <summary>
	/// Called when the server enters hibernation.
	/// </summary>
	void OnServerEnterHibernation() { }

	/// <summary>
	/// Called when the server exits hibernation.
	/// </summary>
	void OnServerExitHibernation() { }
};
