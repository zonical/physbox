using System.Threading.Tasks;
using Sandbox;

public class GenerateNavMeshSystem : GameObjectSystem, ISceneLoadingEvents
{
	public GenerateNavMeshSystem( Scene scene ) : base( scene )
	{

	}

	public Task OnLoad( Scene scene, SceneLoadOptions options )
	{
		if ( scene.IsEditor ) return Task.CompletedTask;
		if ( PhysboxUtilites.IsMainMenuScene() ) return Task.CompletedTask;
		if ( !Networking.IsHost ) return Task.CompletedTask;

		LoadingScreen.Title = "Generating NavMesh";
		
		// Generate Navmesh if one isn't enabled.
		if ( !scene.NavMesh.IsEnabled )
		{
			Log.Warning( "NavMesh was not found for this map. Generating one now!" );
			scene.NavMesh.IsEnabled = true;
			scene.NavMesh.ExcludedBodies.Add( PhysboxConstants.HitboxTag );
			scene.NavMesh.Generate( scene.PhysicsWorld );
		}

		return Task.CompletedTask;
	}

	public void AfterLoad( Scene scene ) { }
}
