using Editor.Assets;
using Sandbox;
using System.Threading.Tasks;

[AssetPreview( "pdef" )]
class PropDefinitionPreview : AssetPreview
{
	// The speed at which the model rotates. The length of a cycle in seconds is 1 / CycleSpeed
	public override float PreviewWidgetCycleSpeed => 0.2f;

	// This will evaluate each frame and pick the one with the least alpha and most luminance
	public override bool UsePixelEvaluatorForThumbs => true;

	public PropDefinitionPreview( Asset asset ) : base( asset )
	{
	}

	public override async Task InitializeAsset()
	{
		await Task.Yield();

		var prop = Asset.LoadResource<PropDefinitionResource>();
		var model = prop.Model;
		if ( model is null )
			return;

		// Create the SceneObject, and position the Camera to fit its bounds
		using ( Scene.Push() )
		{
			PrimaryObject = new GameObject();
			PrimaryObject.AddComponent<ModelRenderer>().Model = model;
		}

		SceneSize = model.RenderBounds.Size;
		SceneCenter = model.RenderBounds.Center;
	}
}
