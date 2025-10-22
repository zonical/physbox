using Sandbox;
using System;
using Sandbox.UI;
using System.Reflection.Metadata.Ecma335;

[Group( "Physbox" )]
[Title( "Listen for Collisions" )]
[Icon( "exit_to_app" )]
[Tint( EditorTint.Yellow )]
public sealed class ObjectCollisionListenerComponent : Component, Component.ICollisionListener
{
	[Description( "This does not need to be set for most purposes. See PlayerComponent.CreateHitbox()" )]
	[Property, Sync] public GameObject CollisionProxy { get; set; }
	[Property] public TagSet IgnoreTags = new() 
	{ 
		PhysboxConstants.HeldPropTag, 
		PhysboxConstants.DebrisTag, 
		PhysboxConstants.IgnoreBreakablePropTag 
	};
	[Property, ReadOnly] public List<Guid> RecentlyHitBy = new();

	public void OnCollisionStart( Collision collision )
	{
		if ( IsProxy ) return;

		// Reuse our collision proxy.
		var self = collision.Self.GameObject;
		if ( CollisionProxy is not null )
		{
			self = CollisionProxy;
		}

		// See if the thing colliding with us has their own collision proxy.
		var other = collision.Other.GameObject;
		var otherCollider = other.Components.Get<ObjectCollisionListenerComponent>( 
			FindMode.Enabled | 
			FindMode.InSelf | 
			FindMode.InAncestors | 
			FindMode.InChildren );
		if ( otherCollider?.CollisionProxy is not null )
		{
			other = otherCollider.CollisionProxy;
		}

		if ( RecentlyHitBy.Contains( other.Id ) ) return;

		// Ignore tags that we hate.
		foreach ( var tag in IgnoreTags )
		{
			if ( other.Tags.Contains( tag ) ) return;
		}

		// Don't register collisions with the thing we are touching for a while.
		RecentlyHitBy.Add( other.Id );
		Invoke( 3.0f, () => RecentlyHitBy.Remove( other.Id ) );

		var speed = float.Abs( collision.Contact.NormalSpeed );

		var collisionProcessor = Scene.GetSystem<ObjectCollisionProcessorSystem>();
		collisionProcessor.RegisterCollisionEvent( self, other, speed );

		return;
	}

	[Rpc.Broadcast]
	public void DestroyThisProp( GameObject prop )
	{
		prop.Destroy();
	}
}
