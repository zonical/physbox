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
	private Collider Collider => GetComponent<Collider>();

	protected override void OnStart()
	{
		GameObject.Tags.Add( "trigger" );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || Collider is null || !Collider.IsValid() ) return;

		foreach ( var player in
				 Collider.Touching.SelectMany( (Func<Collider, IEnumerable<PlayerComponent>>)(
					 x => x.GetComponentsInParent<PlayerComponent>().Distinct()) )
				 )
		{
			if ( player.IsBot ) continue;
			var playerController = player.PlayerController;

			var rigid = player.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndChildren );
			rigid.Velocity += LaunchDirection.Forward * Speed;
			rigid.AngularVelocity += LaunchDirection.Forward * Speed;
		}
	}

	protected override void DrawGizmos()
	{
		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Arrow( Vector3.Zero, Vector3.Zero + (LaunchDirection.Forward * 128) );
			if ( Components.TryGet<MeshComponent>( out var mesh ) )
			{
				Gizmo.Draw.LineBBox( mesh.Model.Bounds );
			}
		}
	}
}
