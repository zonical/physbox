using System.Threading.Tasks;

public interface IDeleteInMainMenu
{
}

public class GameWarmupSystem : GameObjectSystem, ISceneLoadingEvents
{
	[SkipHotload] private static readonly HashSet<string> LoadedAssets = new();
	private Scene scene;


	public GameWarmupSystem( Scene scene ) : base( scene )
	{
	}

	private List<string> WarmupAssets = new() { "facepunch.explosionmedium" };

	public Task OnLoad( Scene scene, SceneLoadOptions options )
	{
		if ( scene.IsEditor )
		{
			return Task.CompletedTask;
		}

		// Do NOT load the system scene in the main menu. It fucks everything up
		// and causes the game to hang (for some reason). We get to disable this
		// just in time as the system scene is added after this scene has
		// finished loading.
		if ( PhysboxUtilites.IsMainMenuScene() )
		{
			scene.WantsSystemScene = false;
		}

		return Task.CompletedTask;
	}

	public void AfterLoad( Scene scene )
	{
		this.scene = scene;
		Warmup();
	}

	private void Warmup()
	{
		_ = WarmupCloudAssets();
		_ = GenerateNavmesh();
	}

	private async Task WarmupCloudAssets()
	{
		LoadingScreen.Title = "Cloud Asset Warmup";

		/* TODO: Once we get multiple resources here (hopefully defined in a file),
		can we get them to load in parallel?*/
		foreach ( var asset in WarmupAssets )
		{
			await WarmupAsset( asset );
		}
	}

	private async Task WarmupAsset( string ident )
	{
		if ( LoadedAssets.Contains( ident ) )
		{
			return;
		}

		LoadingScreen.Title = $"Cloud Asset Warmup - {ident}";
		var package = await Package.Fetch( ident, false );
		if ( package == null || package.Revision == null )
		{
			Log.Error( $"Could not find package {ident}" );
			return;
		}

		await package.MountAsync();

		var primaryAsset = package.GetMeta( "PrimaryAsset", "" );
		Log.Info( $"GameWarmupSystem - Warmed up cloud asset {ident} ({primaryAsset})" );

		LoadedAssets.Add( ident );
	}

	private async Task GenerateNavmesh()
	{
		LoadingScreen.Title = "Generating NavMesh";

		if ( PhysboxUtilites.IsMainMenuScene() )
		{
			return;
		}

		// Generate Navmesh if one isn't enabled.
		if ( !scene.NavMesh.IsEnabled )
		{
			Log.Warning( "GameWarmupSystem - NavMesh was not found for this map. Generating one now!" );
			scene.NavMesh.IsEnabled = true;
			await scene.NavMesh.Generate( scene.PhysicsWorld );
			Log.Info( "GameWarmupSystem - NavMesh generated!" );
		}
		else
		{
			Log.Info( "GameWarmupSystem - Navmesh already exists." );
		}

		scene.NavMesh.ExcludedBodies.Add( PhysboxConstants.HitboxTag );
		scene.NavMesh.ExcludedBodies.Add( "clothing" );
	}
}
