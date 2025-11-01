using Sandbox;

public class InstallGameLogicSystem : GameObjectSystem, ISceneLoadingEvents
{
	public InstallGameLogicSystem( Scene scene ) : base( scene )
	{

	}

	public void AfterLoad( Scene scene )
	{
		if ( Scene.IsEditor ) return;

		if ( scene.Get<GameLogicComponent>() is null )
		{
			Log.Info( "Creating GameLogicComponent." );
			var go = new GameObject( "Game Manager" );
			go.NetworkMode = NetworkMode.Object;

			go.AddComponent<GameLogicComponent>();
			go.AddComponent<KillfeedManagerComponent>();

			go.NetworkSpawn();
		}
	}
}
