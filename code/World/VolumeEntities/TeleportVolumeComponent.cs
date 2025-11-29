using Sandbox;
using System.Collections.Generic;

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
			var player = other.Components.Get<PlayerComponent>( FindMode.EverythingInSelfAndAncestors );
			var playerController = player.PlayerController;

			player.WorldPosition = Destination.WorldPosition;
			player.Network.ClearInterpolation();
			playerController.EyeAngles = Destination.WorldRotation;

			var rigid = player.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndChildren );
			rigid.Velocity = (playerController.Velocity * 2) + (LaunchDirection.Forward * Speed);
			rigid.AngularVelocity = (playerController.Velocity * 2) + (LaunchDirection.Forward * Speed);

			// If we are a bot, stop moving to our destination and forget everything.
			if ( player.IsBot && player.Components.TryGet<BotPlayerTasksComponent>( out var bot ) )
			{
				bot.Agent.Stop();
				bot.Agent.SetAgentPosition( Destination.WorldPosition );
			}
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
