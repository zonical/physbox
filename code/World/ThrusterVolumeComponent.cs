using System;
using Sandbox;
using System.Collections.Generic;

[Group( "Physbox" )]
[Title( "Thruster" )]
[Icon( "arrow_outward" )]
[Tint( EditorTint.Yellow )]
public sealed class ThrusterVolumeComponent : Component, Component.ITriggerListener
{
	[Property] public Angles LaunchDirection = Angles.Zero;
	[Property] public int Speed = 200;
	[Property] public bool IgnorePlayers = false;
	[Property] public bool IgnoreProps = false;
	private Collider Collider => GetComponent<Collider>();

	protected override void OnStart()
	{
		GameObject.Tags.Add( "trigger" );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || Collider is null || !Collider.IsValid() )
		{
			return;
		}

		foreach ( var go in
		         Collider.Touching.Select( (Func<Collider, GameObject>)(
			         x => x.GameObject) )
		        )
		{
			var shouldApplyForce = false;

			// If we are a bot, do not thrust upwards. Let Navmesh Links
			// handle the movement for us.
			if ( go.Components.TryGet<PlayerComponent>( out var player,
				    FindMode.Enabled | FindMode.InSelf | FindMode.InAncestors | FindMode.InDescendants ) )
			{
				shouldApplyForce = !IgnorePlayers && player.IsPlayer;
			}
			else if ( go.Tags.Contains( PhysboxConstants.BreakablePropTag ) )
			{
				shouldApplyForce = !IgnoreProps;
			}

			// Thrust!
			if ( shouldApplyForce )
			{
				var rigidBody = go.Components.Get<Rigidbody>( FindMode.Enabled | FindMode.InSelf |
				                                              FindMode.InAncestors | FindMode.InDescendants );
				rigidBody?.Velocity += LaunchDirection.Forward * Speed;
				rigidBody?.AngularVelocity += LaunchDirection.Forward * Speed;
			}
		}
	}

	protected override void DrawGizmos()
	{
		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Arrow( Vector3.Zero, Vector3.Zero + LaunchDirection.Forward * 128 );
			if ( Components.TryGet<MeshComponent>( out var mesh ) )
			{
				Gizmo.Draw.LineBBox( mesh.Model.Bounds );
			}
		}
	}
}
