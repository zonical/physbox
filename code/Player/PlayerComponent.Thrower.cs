using System;
using System.Threading;
using Physbox;
using Sandbox.Audio;
using Sandbox.Diagnostics;
using Sandbox.Services;

public partial class PlayerComponent
{
	private Angles AdditionalPropRotation;


	[Property] [Feature( "Thrower" )] public Gradient ForceColorGradient = new();
	[Property] [Feature( "Thrower" )] public float BuiltUpForce;
	[Property] [Feature( "Thrower" )] public float ForcePerFrame = 0.01f;
	[Property] [Feature( "Thrower" )] public Vector3 HeldObjectOffset = new();
	[Property] [Feature( "Thrower" )] public float MaxForce = 2.75f;

	private CancellationToken PropCancellationToken = CancellationToken.None;
	private CancellationTokenSource PropCancellationTokenSource;

	public int Throws;

	// ==================== [ PROPERTIES ] ====================
	[Property]
	[Feature( "Thrower" )]
	[Title( "Object Currently Held" )]
	[Sync]
	public GameObject HeldGameObject { get; set; }

	[Property]
	[Feature( "Thrower" )]
	[Title( "Object Under Crosshair" )]
	[Sync]
	public GameObject CurrentlyLookingAtObject { get; set; }

	[Property]
	[Feature( "Thrower" )]
	[Title( "Last Held Object" )]
	[Sync]
	public GameObject LastHeldGameObject { get; set; }

	[Property] [Feature( "Thrower" )] public bool CanPickupObjects { get; private set; } = true;
	[Property] [Feature( "Thrower" )] public bool CanThrowObject { get; private set; } = true;

	// ==================== [ VARIABLES ] ====================
	private PropDefinitionResource HeldProp =>
		HeldGameObject?.Components.Get<PropDefinitionComponent>( true ).Definition;

	/// <summary>
	/// Draws the crosshair. We don't need a fancy Razor panel for this.
	/// </summary>
	private void DrawCrosshair()
	{
		if ( Hud is null || !Hud.Enabled || !HudRoot.DrawHud || Camera is null )
		{
			return;
		}

		var color = CurrentlyLookingAtObject is null ? Color.White : Color.Yellow;
		Camera.Hud.DrawCircle( Screen.Size / 2, 4, color );
	}

	private void HandleThrowerInput()
	{
		if ( IsProxy || !IsAlive )
		{
			return;
		}

		if ( Input.Pressed( "attack1" ) )
		{
			if ( HeldGameObject is null )
			{
				FindNewTarget();
			}
			else
			{
				if ( CanThrowObject )
				{
					ThrowHeldObject();
					AdditionalPropRotation = new Angles();

					Throws++;
				}
				else
				{
					PhysboxUtilites.SendLocalChatMessage( MessageType.System,
						"Cannot throw prop. Too close to a wall or another object." );
					Sound.Play( "sounds/player_use_fail.sound", Mixer.FindMixerByName( "UI" ) );
				}
			}
		}

		// Drop an object if we are holding one.
		if ( Input.Pressed( "drop" ) && HeldGameObject is not null && CanThrowObject )
		{
			DropObject();
			AdditionalPropRotation = new Angles();
		}

		// Rotate this object.
		if ( Input.Down( "pan_right" ) && HeldGameObject is not null )
		{
			AdditionalPropRotation += new Angles( 0, 10, 0 );
		}

		if ( Input.Down( "pan_left" ) && HeldGameObject is not null )
		{
			AdditionalPropRotation += new Angles( 0, -10, 0 );
		}
	}

	private void PositionHeldObject()
	{
		if ( IsProxy )
		{
			return;
		}

		if ( IsPlayer )
		{
			var cameraOffset = Camera.WorldPosition - new Vector3( 0, 0, 16 ) + Camera.WorldRotation.Forward * 64;
			var heldPositionOffset = HeldProp?.HeldPositionOffset ?? Vector3.Zero;
			var propOffset = heldPositionOffset.RotateAround( Vector3.Zero, Camera.WorldRotation );
			var targetPos = cameraOffset + propOffset;

			var heldRotationOffset = HeldProp?.HeldRotationOffset.ToRotation() ?? new Rotation();

			var rotation = Camera.WorldRotation * heldRotationOffset * AdditionalPropRotation.ToRotation();

			HeldGameObject.WorldPosition = targetPos;
			HeldGameObject.WorldRotation = rotation;
		}
		else if ( IsBot )
		{
			Assert.IsValid( HeldProp );

			var zOffset = 56; // 72 (eye height) - 16
			var targetPos = WorldPosition + new Vector3( 0, 0, zOffset ) + WorldRotation.Forward * 64;
			var heldPositionOffset = HeldProp?.HeldPositionOffset ?? Vector3.Zero;
			var propOffset = heldPositionOffset.RotateAround( Vector3.Zero, WorldRotation );
			HeldGameObject.WorldPosition = targetPos + propOffset;
		}
	}

	private void FindPotentialTarget()
	{
		if ( IsProxy )
		{
			return;
		}

		var trace = GenerateTrace();

		if ( CurrentlyLookingAtObject is not null )
		{
			CurrentlyLookingAtObject.Components.Get<HighlightOutline>()?.Destroy();
			CurrentlyLookingAtObject = null;
		}

		var HaveFacepunchFuckedUpHighlightsAgain = true;

		// We've found something valid, put a highlight on it.
		if ( trace.GameObject is not null && (
			    trace.GameObject.Tags.Contains( PhysboxConstants.BreakablePropTag ) ||
			    trace.GameObject.GetComponent<WorldLifeComponent>() is not null) )
		{
			//Log.Info( $"Thrower - looking at {trace.GameObject}" );

			CurrentlyLookingAtObject = trace.GameObject;
			if ( !HaveFacepunchFuckedUpHighlightsAgain )
			{
				var outline = CurrentlyLookingAtObject.AddComponent<HighlightOutline>();

				// Do not network this outline.
				outline.Flags = outline.Flags | ComponentFlags.NotNetworked;
			}
		}
	}

	private void FindNewTarget()
	{
		if ( IsProxy )
		{
			return;
		}

		var trace = GenerateTrace();

		// We've found something valid, pick it up.
		if ( trace.GameObject is not null && trace.GameObject.Tags.Contains( PhysboxConstants.BreakablePropTag ) )
		{
			PickupObject( trace.GameObject );
		}
	}

	public void PickupObject( GameObject go )
	{
		MakeGameObjectHeldObject( go );
		BroadcastPickupAnimation();
		CancelPreviousOwnerRemovalProcess();
		UpdateSpeedFromHeldObject();
		UpdateHeldObjectComponents();

		if ( IsPlayer )
		{
			Stats.Increment( PhysboxConstants.PropsPickedUpStat, 1 );
		}
	}

	private void MakeGameObjectHeldObject( GameObject go )
	{
		go.SetParent( GameObject );
		HeldGameObject = go;

		HeldGameObject.Network.AssignOwnership( Network.Owner );
		HeldGameObject.Tags.Add( PhysboxConstants.HeldPropTag );

		if ( HeldGameObject.Components.TryGet<PropLifeComponent>( out var propLifeComponent ) )
		{
			propLifeComponent.LastOwnedBy = this;
		}
	}

	private void CancelPreviousOwnerRemovalProcess()
	{
		// Cancel Invoke token if we're picking up this prop again.
		if ( HeldGameObject.Id == LastHeldGameObject?.Id &&
		     PropCancellationToken != CancellationToken.None &&
		     PropCancellationToken.CanBeCanceled )
		{
			//Log.Info( "Cancelled." );
			PropCancellationTokenSource.Cancel();

			PropCancellationTokenSource?.Dispose();
			PropCancellationToken = CancellationToken.None;
		}
	}

	private void UpdateSpeedFromHeldObject()
	{
		// Alter our speed.
		if ( !PlayerConvars.SpeedAffectedByMass || !IsPlayer )
		{
			return;
		}

		var subtractAmount = float.Round( float.Sqrt( HeldProp.Mass ) * 10 );

		PlayerController.RunSpeed = float.Max( PlayerController.RunSpeed - subtractAmount, 50 );
		PlayerController.WalkSpeed = float.Max( PlayerController.WalkSpeed - subtractAmount, 30 );
		PlayerController.DuckedSpeed = float.Max( PlayerController.DuckedSpeed - subtractAmount, 20 );
	}

	private void UpdateHeldObjectComponents()
	{
		// Make this object slightly translucent so we can see through it.
		if ( HeldGameObject.Components.TryGet<ModelRenderer>( out var modelRen ) )
		{
			BroadcastModelTintChange( modelRen, 0.5f );
		}

		// Don't let this object become affected by gravity.
		if ( HeldGameObject.Components.TryGet<Rigidbody>( out var rigidBody ) )
		{
			rigidBody.Sleeping = true;
			rigidBody.Gravity = false;
		}

		// Turn off all colliders.
		foreach ( var collider in HeldGameObject.GetComponents<Collider>() )
		{
			collider.Enabled = false;
			collider.ColliderFlags = ColliderFlags.IgnoreTraces;
		}
	}

	[Rpc.Broadcast]
	private void BroadcastPickupAnimation()
	{
		var renderer = PlayerController.Renderer;
		renderer.Parameters.Set( "holdtype", 4 );
		renderer.Parameters.Set( "holdtype_pose", 3 );
	}

	[Rpc.Broadcast]
	private void BroadcastPutDownAnimation()
	{
		var renderer = PlayerController.Renderer;
		renderer.Parameters.Set( "holdtype", 0 );
		renderer.Parameters.Set( "holdtype_pose", 0 );
	}

	[Rpc.Broadcast]
	private void BroadcastModelTintChange( ModelRenderer ren, float tint )
	{
		ren.Tint = Color.White.WithAlpha( tint );
		//ren.SetMaterial( Material.FromShader( "shaders/charge.shader" ) );
	}

	private Vector3 CalculateThrowVelocity( float mass )
	{
		var forward = IsPlayer ? Camera.WorldRotation.Forward : WorldRotation.Forward;
		return forward * (MathF.Sqrt( (500 - mass) * 1000 ) * BuiltUpForce);
	}

	private GameObject FreeAndReturnHeldObject()
	{
		if ( HeldGameObject is null )
		{
			return null;
		}

		BroadcastPutDownAnimation();

		HeldGameObject.SetParent( null );
		HeldGameObject.Tags.Remove( PhysboxConstants.HeldPropTag );
		LastHeldGameObject = HeldGameObject;
		HeldGameObject = null;

		StartPreviousOwnerRemovalProcess();

		return LastHeldGameObject;
	}

	private void StartPreviousOwnerRemovalProcess()
	{
		// Start a new cancellation token.
		PropCancellationTokenSource = new CancellationTokenSource();
		PropCancellationToken = PropCancellationTokenSource.Token;

		// After a few seconds, set ourselves to not own this object anymore.
		Invoke( 5.0f, () => RemovePreviousOwner( LastHeldGameObject ), PropCancellationToken );
	}

	private void RemovePreviousOwner( GameObject go )
	{
		if ( go is not null && go.Components.TryGet<PropLifeComponent>( out var propLifeComponent ) )
		{
			//Log.Info( $"No longer owned by {propLifeComponent.LastOwnedBy}" );
			propLifeComponent.LastOwnedBy = null;
		}
	}

	private void RestoreFormallyHeldObject( GameObject go )
	{
		if ( go.Components.TryGet<ModelRenderer>( out var modelRen ) )
		{
			BroadcastModelTintChange( modelRen, 1 );
		}

		foreach ( var collider in go.GetComponents<Collider>( true ) )
		{
			collider.Enabled = true;
			collider.ColliderFlags = 0;
		}

		if ( go.Components.TryGet<Rigidbody>( out var rigidBody ) )
		{
			rigidBody.Sleeping = false;
			rigidBody.Gravity = true;
		}

		if ( go.Components.TryGet<HighlightOutline>( out var highlightOutline ) )
		{
			highlightOutline.Destroy();
		}
	}

	public void ThrowHeldObject()
	{
		if ( IsProxy )
		{
			return;
		}

		var go = FreeAndReturnHeldObject();
		RestoreFormallyHeldObject( go );
		FlingGameObject( go );
		TriggerViewmodelThrowAnimation();

		if ( IsPlayer )
		{
			Sound.Play( "sounds/player/swoosh.sound" );
			ResetSpeed();
		}
	}

	private void FlingGameObject( GameObject go )
	{
		// Fling it!
		if ( !go.Components.TryGet<Rigidbody>( out var rigidBody ) )
		{
			return;
		}

		var mass = rigidBody.Mass;

		// Slightly offset where we throw.
		var dir = CalculateThrowVelocity( mass );

		rigidBody.Sleeping = false;
		rigidBody.Gravity = true;
		rigidBody.Velocity = dir;

		BuiltUpForce = 0;
	}

	public void DropObject()
	{
		if ( IsProxy )
		{
			return;
		}

		if ( HeldGameObject is null )
		{
			return;
		}

		var go = FreeAndReturnHeldObject();
		RestoreFormallyHeldObject( go );
		ResetSpeed();

		BuiltUpForce = 0;
	}

	private float CalculateDistanceDroppedForPreview( float distance )
	{
		// pos is where we WOULD end up. But we need to account for gravity.
		// distance = velocity / time
		// time = distance / velocity
		// distanceDropped = 0.5 * gravity * time�

		var velocity = CalculateThrowVelocity( HeldProp.Mass );
		var time = distance / velocity.Length;

		// Draw some debug stuff.
		if ( PhysboxDebug.DebugThrowerPreview )
		{
			Camera.Hud.DrawText( $"Thrower Preview Debug\n" +
			                     $"Distance: {distance}\n" +
			                     $"Velocity: {velocity} (len: {velocity.Length})\n" +
			                     $"Time: {time}\n" +
			                     $"Eq: 0.5 * {Scene.PhysicsWorld.Gravity.z} * {time * time}", 16, Color.White,
				Screen.Size * 0.05 );
		}

		// I swear to god if we EVER need to account for any other gravity
		// besides "down", I will say some very not nice words.
		// This actually gives us a VERY accurate position. Not 100% perfect
		// compared to actually simulating it, but s&box doesn't have that
		// ability yet as far as I'm aware.
		return 0.5f * Scene.PhysicsWorld.Gravity.z * (time * time);
	}

	/*private void PositionPreview()
	{
		if ( IsProxy ) return;
		if ( Preview is null || !Preview.IsValid() ) return;
		//if ( PreviewLine is null || !PreviewLine.IsValid() ) return;

		var trace = GeneratePreviewTrace(); // Should probably make this a box trace.
		var pos = trace.Hit ? trace.HitPosition : trace.EndPosition;
		var distanceDropped = CalculateDistanceDroppedForPreview( trace.Distance );

		// All of this math, just to move the preview up and down a few units :) #worthit
		var center = Preview.Model.Bounds.Center;
		var finalPosTrans = new Transform( new Vector3( pos.x, pos.y, pos.z + distanceDropped - center.z ) );

		Preview.Transform = finalPosTrans;
		Preview.Rotation = HeldGameObject.WorldRotation;
		Preview.RenderingEnabled = CanThrowObject;
	}*/

	private void ThrowCheck()
	{
		if ( IsProxy )
		{
			return;
		}

		var model = HeldGameObject.GetComponent<ModelRenderer>().Model ?? Model.Cube;
		var trace = GenerateValidThrowTrace( model );

		CanThrowObject = !(trace.Hit || trace.StartedSolid);

		var highlight = HeldGameObject.GetComponent<HighlightOutline>();
		if ( highlight is not null )
		{
			if ( CanThrowObject )
			{
				// Set highlight accordingly.
				highlight.Color = Color.White;
				highlight.ObscuredColor = Color.Red;
				highlight.InsideObscuredColor = Color.Red.WithAlpha( 0.5f );
			}
			else
			{
				highlight.Color = Color.Green;
				highlight.ObscuredColor = Color.White.WithAlpha( 0.1f );
			}
		}
	}

	private void ResetSpeed()
	{
		if ( IsProxy )
		{
			return;
		}

		PlayerController.RunSpeed = PlayerConvars.RunSpeed;
		PlayerController.WalkSpeed = PlayerConvars.WalkSpeed;
		PlayerController.DuckedSpeed = PlayerConvars.DuckedSpeed;
	}

	private SceneTraceResult GenerateTrace()
	{
		var vector = Camera is not null ? Camera.WorldPosition : Vector3.Zero;
		var ray = new Ray( vector, Camera?.WorldRotation.Forward ?? Vector3.Zero );

		var trace = Scene.Trace.Ray( ray, 64 * 3 )
			.WithoutTags(
				PhysboxConstants.PlayerTag,
				PhysboxConstants.DebrisTag,
				PhysboxConstants.HeldPropTag,
				PhysboxConstants.RagdollTag )
			.Run();

		return trace;
	}

	private SceneTraceResult GeneratePreviewTrace()
	{
		var vector = Camera is not null ? Camera.WorldPosition : Vector3.Zero;
		var ray = new Ray( vector, Camera?.WorldRotation.Forward ?? Vector3.Zero );

		var trace = Scene.Trace.Ray( ray, 4096 )
			.WithoutTags(
				PhysboxConstants.DebrisTag,
				PhysboxConstants.HeldPropTag )
			.IgnoreGameObject( HeldGameObject )
			.IgnoreGameObject( GameObject )
			.Run();

		return trace;
	}

	private SceneTraceResult GenerateValidThrowTrace( Model model )
	{
		var ray = new Ray( Camera.WorldPosition + Camera.WorldRotation.Forward * 8, Camera.WorldRotation.Forward );

		var trace = Scene.Trace.Box(
				model.Bounds,
				ray,
				72 + (!PlayerController.IsOnGround ? 64 : 0) ) // this 192 number may not be perfect.
			.WithoutTags(
				PhysboxConstants.DebrisTag,
				PhysboxConstants.HeldPropTag,
				PhysboxConstants.PlayerTag )
			.IgnoreGameObject( HeldGameObject )
			.IgnoreGameObject( GameObject )
			.Run();

		return trace;
	}
}
