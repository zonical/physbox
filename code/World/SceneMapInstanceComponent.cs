using System.Threading.Tasks;
namespace Sandbox;

[Group( "Physbox" )]
[Title( "Scene Map Instance" )]
[Icon( "map" )]
[Tint( EditorTint.Yellow )]
public sealed class SceneMapInstanceComponent : Component, Component.ExecuteInEditor
{
	[Property, Title("Map"), MapAssetPath]
	public string MapName { get; set; }
	
	protected override async Task OnLoad()
	{
		// Do not load if the scene already has a MapInstance present.
		// There is probably a better way to do this, as we need to account
		// for scenemaps that don't contain a MapInstance component, but
		// this works for now.
		if ( Scene.Get<MapInstance>() is not null ) return;
		
		Log.Info( $"Loading scene map {MapName}" );
		LoadingScreen.Title = $"Loading - {MapName}";

		var package = await Package.Fetch( MapName, false );
		if( package == null || package.Revision == null )
		{
			Log.Error( $"Could not find package {MapName}" );
			return;
		}
		
		await package.MountAsync();

		var primaryAsset = package.GetMeta( "PrimaryAsset", "" );
		var file = await ResourceLibrary.LoadAsync<SceneFile>( primaryAsset );

		var slo = new SceneLoadOptions() { ShowLoadingScreen = true, IsAdditive = true };
		slo.SetScene( file );
		var scene = Scene.Load( slo );
	}
}
