using System.Threading.Tasks;
using Sandbox;

[Group( "Physbox" )]
[Title( "Game Networking Component" )]
[Icon( "wifi" )]
[Tint( EditorTint.Yellow )]
[Hide]
public class GameNetworkingComponent : Component, Component.INetworkListener
{
	// Should the server go into hibernation if there are no real players online.
	[ConVar( "pb_hibernate", ConVarFlags.Replicated | ConVarFlags.Hidden )]
	public static bool ShouldHibernate { get; set; } = true;

	public static bool IsHibernating { get; set; } = true;
	private List<Connection> _playerConnections { get; set; } = [];

	/// <summary>
	/// Enters hibernation if enabled.
	/// </summary>
	protected override void OnStart()
	{
		IsHibernating = ShouldHibernate;

		// Bring us immediately out of hibernation.
		if ( !IsHibernating )
		{
			Scene.RunEvent<IPhysboxNetworkEvents>( x => x.OnServerExitHibernation() );
		}
	}

	/// <summary>
	/// Creates a lobby if we don't already have one by this point.
	/// </summary>
	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor || PhysboxUtilities.IsMainMenuScene() )
		{
			return;
		}

		if ( !Networking.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";
			Log.Info( "Lobby doesn't exist - creating a new one!" );

			await Task.DelayRealtimeSeconds( 0.1f );
			PhysboxUtilities.CreateNewLobby();

			// If we are not on a map for some reason, load into Street.
			if ( Application.IsDedicatedServer && Scene.Get<MapInformationComponent>() is null )
			{
				SetMap( Connection.Host, "street" );
			}
		}
	}

	/// <summary>
	/// Called when a player has joined the lobby.
	/// </summary>
	/// <param name="channel">Incoming connection.</param>
	public void OnActive( Connection channel )
	{
		Log.Info( $"GameNetworkingComponent - Player '{channel.DisplayName}' has joined the game" );

		_playerConnections.Add( channel );
		Scene.RunEvent<IPhysboxNetworkEvents>( x => x.OnPlayerConnected( channel ) );

		// Bring us out of hibernation.
		if ( IsHibernating )
		{
			IsHibernating = false;
			Scene.RunEvent<IPhysboxNetworkEvents>( x => x.OnServerExitHibernation() );
			Log.Info( "GameNetworkingComponent - Exiting hibernation." );
		}
	}

	/// <summary>
	/// Called when a player has left the lobby.
	/// </summary>
	/// <param name="channel"></param>
	public void OnDisconnected( Connection channel )
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		Log.Info( $"GameNetworkingComponent - Player '{channel.DisplayName}' has left the game" );
		_playerConnections.Remove( channel );

		// If the only players that are remaining are bots, go into hibernation.
		if ( ShouldHibernate && _playerConnections.Count == 0 )
		{
			// There shouldn't be any connections left except for bots
			// (if there are any).
			foreach ( var conn in Connection.All )
			{
				conn.Kick( "Entering hibernation." );
			}

			IsHibernating = true;
			Scene.RunEvent<IPhysboxNetworkEvents>( x => x.OnServerEnterHibernation() );
			Log.Info( "GameNetworkingComponent - Entering hibernation." );
		}
	}

	/// <summary>
	/// Changes the map.
	/// </summary>
	/// <param name="caller">Connection calling the ConCmd.</param>
	/// <param name="mapName">Partial or whole path to the map.</param>
	[ConCmd( "pb_changemap" )]
	public static void SetMap( Connection caller, string mapName )
	{
		if ( caller != Connection.Host )
		{
			return;
		}

		// String sanity checks.
		if ( !mapName.StartsWith( "scenes/maps" ) )
		{
			mapName = "scenes/maps/" + mapName;
		}

		if ( !mapName.EndsWith( ".scene" ) )
		{
			mapName += ".scene";
		}

		var file = ResourceLibrary.Get<SceneFile>( mapName );
		if ( file is null )
		{
			Log.Error( $"Could not find map {mapName}." );
			return;
		}

		// Start a new lobby if we don't have one yet.
		if ( Networking.IsActive )
		{
			Networking.Disconnect();
		}

		PhysboxUtilities.CreateNewLobby();

		// Change to our new scene.
		var slo = new SceneLoadOptions { ShowLoadingScreen = true };
		slo.SetScene( file );
		Game.ChangeScene( slo );
	}
}
