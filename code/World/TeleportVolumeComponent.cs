using Sandbox;

[Group( "Physbox" )]
[Title( "Teleporter" )]
[Icon( "arrow_outward" )]
[Tint( EditorTint.Yellow )]
public sealed class TeleportVolumeComponent : Component, Component.ITriggerListener
{
	[Property] public GameObject Destination;
	[Property] public Angles LaunchDirection;
	[Property] public int Speed = 200;

	protected override void OnStart()
	{
		GameObject.Tags.Add( "trigger" );
	}

	public void OnTriggerEnter( GameObject other )
	{
		if ( other.Tags.Contains( PhysboxConstants.PlayerTag ) && Destination is not null )
		{
			var playerController = other.Components.Get<PlayerController>( FindMode.EverythingInSelfAndAncestors );

			playerController.GameObject.WorldPosition = Destination.WorldPosition;
			playerController.EyeAngles = Destination.WorldRotation;
			playerController.Network.ClearInterpolation();

			var rigid = playerController.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndChildren );
			rigid.Velocity = (playerController.Velocity * 2) + (LaunchDirection.Forward * Speed);
			rigid.AngularVelocity = (playerController.Velocity * 2) + (LaunchDirection.Forward * Speed);
		}
	}

	protected override void DrawGizmos()
	{
		if ( Gizmo.IsHovered )
		{
		}

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
