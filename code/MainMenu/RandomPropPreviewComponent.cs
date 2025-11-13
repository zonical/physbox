using Physbox;

namespace Sandbox;

[Group( "Physbox" )]
[Title( "Random Prop Preview (Menu)" )]
[Icon( "info" )]
[Tint( EditorTint.Yellow )]

public sealed class RandomPropPreviewComponent : Component
{
	[Property] public SkinnedModelRenderer PlayerPreview { get; set; }
	
	/// <summary>
	/// Creates a prop in front of the player preview.
	/// </summary>
	protected override void OnEnabled()
	{
		var go = new GameObject( "Prop Preview" );
		var modelRenderer = go.AddComponent<ModelRenderer>();
		
		// Select a prop.
		var prop = Game.Random.FromList( ResourceLibrary.GetAll<PropDefinitionResource>().ToList() );
		modelRenderer.Model = prop.Model;
		modelRenderer.Tint = Color.White.WithAlpha( 0.5f );
		
		// Adjust position.
		var eyeOffset = new Vector3( 0, 0, 48 );
		var targetPos = WorldPosition + eyeOffset + (WorldRotation.Forward * 64);
		go.WorldPosition = targetPos;
		go.WorldRotation = WorldRotation * prop.HeldRotationOffset.ToRotation();
		
		// Update player model.
		PlayerPreview.Parameters.Set( "holdtype", 4 );
		PlayerPreview.Parameters.Set( "holdtype_pose", 3 );
	}
}
