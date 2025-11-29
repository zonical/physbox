using System;

namespace Sandbox;

public sealed class RotatingModelPreviewComponent : Component
{
	private float _offset;

	protected override void OnEnabled()
	{
		_offset = Game.Random.Next( 0, 10 );
	}

	protected override void OnUpdate()
	{
		var rotation = new Angles();
		rotation.pitch = (float)Math.Sin( Time.Now - (3 + _offset) ) * 0.1f;
		rotation.yaw = (float)Math.Cos( Time.Now - (2 + _offset) ) * 0.1f;
		rotation.roll = (float)Math.Sin( Time.Now - (1 + _offset) ) * 0.1f;

		WorldRotation *= rotation;

		var position = Vector3.Zero;
		position.z += (float)Math.Sin( Time.Now - (3 + _offset) ) * 0.15f;

		WorldPosition += position;
	}
}
