using Sandbox.Internal;
using System;
using System.Numerics;
using static Sandbox.Component.IPressable;

namespace Sandbox;

public delegate void PressableButtonDelegate( PlayerComponent player );

[Group( "Physbox" )]
[Title( "Pressable Button" )]
[Icon( "radio_button_checked" )]
[Tint( EditorTint.Yellow )]
public sealed class PressableButtonComponent : Component, Component.IPressable
{
	[Property] public PressableButtonDelegate OnButtonPressed { get; set; }
	[Property] public float TimeUntilReset { get; set; } = 1.0f;
	[Property] public bool Locked { get; set; } = false;


	public bool Press( Event e )
	{
		Locked = true;

		var source = e.Source;
		if ( source is not PlayerComponent )
		{
			var player = source.GameObject.GetComponent<PlayerComponent>();
			OnButtonPressed?.Invoke( player );
		}
		else
		{
			OnButtonPressed?.Invoke( source as PlayerComponent );
		}

		Invoke( TimeUntilReset, () => { Locked = false; } );

		return true;
	}

	public bool CanPress( Event e )
	{
		return !Locked;
	}
}
