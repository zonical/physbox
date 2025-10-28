using Sandbox;
using Editor;
using Editor.Assets;
using System;
using System.Threading.Tasks;

[AssetPreview( "pdef" )]
class PropDefinitionPreview : AssetPreview
{
	GameObject PropGameObject;
	ModelRenderer ModelPreview;

	// The speed at which the model rotates. The length of a cycle in seconds is 1 / CycleSpeed
	public override float PreviewWidgetCycleSpeed => 0.1f;

	// This will evaluate each frame and pick the one with the least alpha and most luminance
	public override bool UsePixelEvaluatorForThumbs => true;

	public PropDefinitionPreview( Asset asset ) : base( asset )
	{
	}

	public override async Task InitializeAsset()
	{
		await Task.Yield();

		var resource = Asset.LoadResource<GameResource>();
		var model = resource.GetSerialized().GetProperty( "Model" ).GetValue<Model>( Model.Error );
		if ( model is null )
			return;

		// Create the SceneObject, and position the Camera to fit its bounds
		using ( Scene.Push() )
		{
			PrimaryObject = new GameObject();

			var playerModel = PrimaryObject.AddComponent<SkinnedModelRenderer>();
			playerModel.Model = Model.Load( "models/citizen/citizen.vmdl" );
			playerModel.Parameters.Set( "holdtype", 4 );
			playerModel.Parameters.Set( "holdtype_pose", 3 );
			playerModel.Tint = Color.White.WithAlpha( 0.5f );

			AnimateRendererWithVelocity( Vector3.Forward * 180, playerModel );

			// Create held model.
			PropGameObject = new GameObject();
			ModelPreview = PropGameObject.AddComponent<ModelRenderer>();
			ModelPreview.Model = model;

			PropGameObject.Parent = PrimaryObject;

			var heldPositionOffset = resource.GetSerialized().GetProperty( "HeldPositionOffset" ).GetValue<Vector3>();
			var heldRotationOffset = resource.GetSerialized().GetProperty( "HeldRotationOffset" ).GetValue<Angles>();

			var eyePos = new Vector3( 0, 0, 64 );
			var targetPos = eyePos - new Vector3( 0, 0, 16 ) + (PrimaryObject.WorldRotation.Forward * 64);
			PropGameObject.LocalPosition = targetPos + heldPositionOffset.RotateAround( Vector3.Zero, PrimaryObject.WorldRotation );
			PropGameObject.LocalRotation = PrimaryObject.WorldRotation * heldRotationOffset.ToRotation();

			SceneSize = PrimaryObject.GetBounds().Size;
			SceneCenter = PropGameObject.GetBounds().Center;
		}
	}

	// Stolen from CitizenAnimationHelper
	public void AnimateRendererWithVelocity( Vector3 Velocity, SkinnedModelRenderer Target )
	{
		var dir = Velocity;
		var forward = Target.WorldRotation.Forward.Dot( dir );
		var sideward = Target.WorldRotation.Right.Dot( dir );

		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		Target.Set( "move_direction", angle );
		Target.Set( "move_speed", Velocity.Length );
		Target.Set( "move_groundspeed", Velocity.WithZ( 0 ).Length );
		Target.Set( "move_y", sideward );
		Target.Set( "move_x", forward );
		Target.Set( "move_z", Velocity.z );
	}
}
