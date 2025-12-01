using Sandbox;

public partial class PlayerComponent :
	BaseLifeComponent,
	IGameEvents,
	PlayerController.IEvents
{
	/// <summary>
	/// Creates a viewmodel for the local player.
	/// </summary>
	private void CreateViewmodel()
	{
		// Destroy viewmodel if it currently exists.
		Viewmodel?.Destroy();

		Viewmodel = new GameObject( true, "Viewmodel" );
		Viewmodel.NetworkMode = NetworkMode.Never;
		Viewmodel.Tags.Add( "viewmodel" );

		var modelComp = Viewmodel.AddComponent<SkinnedModelRenderer>();
		modelComp.Model = Cloud.Model( "facepunch.v_first_person_arms_human" );
		modelComp.RenderOptions.Overlay = true;
		modelComp.RenderOptions.Game = false;
		modelComp.RenderType = ModelRenderer.ShadowRenderType.Off;
		modelComp.UseAnimGraph = true;

		Viewmodel.WorldRotation = new Angles( 45, 0, 5 );

		// Parent model to camera.
		Viewmodel.Parent = Camera.GameObject;
	}

	/// <summary>
	/// Updates the position and animations of the viewmodel.
	/// </summary>
	private void UpdateViewmodel()
	{
		if ( Viewmodel is null )
		{
			return;
		}

		Viewmodel.WorldPosition = Camera.WorldPosition - new Vector3( 0, 0, 0 ) + Camera.WorldRotation.Forward * 8;
		Viewmodel.WorldRotation = Camera.WorldRotation * new Angles( 45, 0, 5 );

		var model = Viewmodel.GetComponent<SkinnedModelRenderer>();

		UpdateMoveVelocity( model );
		UpdateBobbing( model );
		UpdateFingers( model );
	}

	private void TriggerViewmodelThrowAnimation()
	{
		if ( Viewmodel is null )
		{
			return;
		}

		var viewmodel = Viewmodel.GetComponent<SkinnedModelRenderer>();
		if ( viewmodel.Enabled )
		{
			viewmodel.Parameters.Set( "b_attack", true );
		}
	}

	/// <summary>
	/// Adjusts the positions/bend of the fingers depending on whether
	/// we are holding a prop or not.
	/// </summary>
	/// <param name="model">Viewmodel component.</param>
	private void UpdateFingers( SkinnedModelRenderer model )
	{
		var left = model.Parameters.GetFloat( "FingerAdjustment_BlendNeutralPose_L" );
		var right = model.Parameters.GetFloat( "FingerAdjustment_BlendNeutralPose_R" );

		model.Parameters.Set( "FingerAdjustment_BlendNeutralPose_L",
			HeldGameObject is not null
				? float.Lerp( left, 1.0f, 10 * Time.Delta )
				: float.Lerp( left, 0f, 10 * Time.Delta ) );
		model.Parameters.Set( "FingerAdjustment_BlendNeutralPose_R",
			HeldGameObject is not null
				? float.Lerp( right, 1.0f, 10 * Time.Delta )
				: float.Lerp( right, 0f, 10 * Time.Delta ) );
	}

	/// <summary>
	/// Applies a movement bob to the viewmodel.
	/// </summary>
	/// <param name="model">Viewmodel component.</param>
	private void UpdateBobbing( SkinnedModelRenderer model )
	{
		var moving = Input.Down( "forward" ) || Input.Down( "left" ) || Input.Down( "right" ) ||
		             Input.Down( "backward" );
		model.Parameters.Set( "move_bob", moving ? 1.0f : 0f );
	}

	/// <summary>
	/// Applies movement velocity to the viewmodel.
	/// </summary>
	/// <param name="model">Viewmodel component.</param>
	private void UpdateMoveVelocity( SkinnedModelRenderer model )
	{
		model.Parameters.Set( "move_x", PlayerController.Velocity.x );
		model.Parameters.Set( "move_y", PlayerController.Velocity.y );
		model.Parameters.Set( "move_z", PlayerController.Velocity.z );
	}

	/// <summary>
	/// Plays the jump animation on the viewmodel.
	/// </summary>
	private void TriggerViewmodelJump()
	{
		var model = Viewmodel.GetComponent<SkinnedModelRenderer>();
		if ( model.Enabled )
		{
			model.Parameters.Set( "b_jump", true );
		}
	}
}
