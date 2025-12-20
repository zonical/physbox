using System;
using Sandbox;
using System.Collections.Generic;

[Group( "Physbox" )]
[Title( "Prop Detector" )]
[Icon( "find_in_page" )]
[Tint( EditorTint.Yellow )]
public sealed class PropDetectionVolumeComponent : Component, Component.ITriggerListener
{
	private Collider Collider => GetComponent<Collider>();
	[Property] public int PropsInVolume { get; set; }

	[Property] public Action<GameObject> OnPropEnter;
	[Property] public Action<GameObject> OnPropExit;

	/// <summary>
	/// Ensure that we have the trigger tag on start.
	/// </summary>
	protected override void OnStart()
	{
		GameObject.Tags.Add( "trigger" );
		Collider.IsTrigger = true;
	}

	/// <summary>
	/// Update the count of props contained within this volume.
	/// </summary>
	protected override void OnFixedUpdate()
	{
		if ( Collider is null || !Collider.IsValid() )
		{
			return;
		}

		PropsInVolume = Collider.Touching.Select( (Func<Collider, GameObject>)(
			x => x.GameObject) ).Count( x => x.Tags.Contains( PhysboxConstants.BreakablePropTag ) );
	}

	/// <summary>
	/// Called when a prop enters the trigger.
	/// </summary>
	/// <param name="other">Prop collider.</param>
	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		var go = other.GameObject;
		if ( go is null || !go.Tags.Contains( PhysboxConstants.BreakablePropTag ) )
		{
			return;
		}

		OnPropEnter?.Invoke( go );
	}

	/// <summary>
	/// Called when a prop exits the trigger.
	/// </summary>
	/// <param name="other">Prop collider.</param>
	void ITriggerListener.OnTriggerExit( Collider other )
	{
		var go = other.GameObject;
		if ( go is null || !go.Tags.Contains( PhysboxConstants.BreakablePropTag ) )
		{
			return;
		}

		OnPropExit?.Invoke( go );
	}
}
