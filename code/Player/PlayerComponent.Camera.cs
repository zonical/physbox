using Physbox;
using System;

public partial class PlayerComponent :
	BaseLifeComponent,
	IGameEvents,
	PlayerController.IEvents,
	Component.INetworkListener
{
	private bool _freeCam = false;
	public bool FreeCam
	{
		get { return _freeCam; }
		set
		{
			_freeCam = value;
			if ( _freeCam == true )
			{
				CreateFreeCam();
				PlayerController.Enabled = false;
			}
			else
			{
				CreateNormalCam();
				PlayerController.Enabled = true;
			}

		}
	}

	private void InitaliseCamera()
	{
		if ( Camera is null )
		{
			var go = new GameObject( true, "Camera" );
			go.WorldPosition = GameObject.WorldPosition;
			go.WorldRotation = GameObject.WorldRotation;

			go.NetworkMode = NetworkMode.Never;
			go.SetParent( GameObject );
			Camera = go.AddComponent<CameraComponent>();
			go.AddComponent<Highlight>();
		}

		Camera.IsMainCamera = true;
		Camera.FieldOfView = Preferences.FieldOfView;
		Camera.BackgroundColor = Color.Black;
	}

	private void CreateFreeCam()
	{
		if ( IsProxy ) return;

		// Detach our camera from ourselves.
		Camera.GameObject.SetParent( null );
	}

	private void CreateNormalCam()
	{
		if ( IsProxy ) return;

		// Reset our camera position.
		Camera.GameObject.WorldPosition = GameObject.WorldPosition;
		Camera.GameObject.WorldRotation = GameObject.WorldRotation;
		Camera.GameObject.SetParent( GameObject );
	}

	private void HandleNoclipMovement()
	{
		Camera.WorldRotation *= Input.AnalogLook;

		/*Camera.WorldRotation *= new Angles(
			Mouse.Delta.y * ( ( Preferences.Sensitivity * 3 )  * Time.Delta ), 
			-Mouse.Delta.x * ( ( Preferences.Sensitivity * 3 ) * Time.Delta ), 
			0).ToRotation();*/

		var angles = Camera.WorldRotation.Angles();
		if ( angles.pitch > 89 ) Camera.WorldRotation = angles.WithPitch( 89 );
		if ( angles.pitch < -89 ) Camera.WorldRotation = angles.WithPitch( -89 );

		if ( angles.yaw > 180 ) Camera.WorldRotation = angles.WithYaw( 180 );
		if ( angles.yaw < -180 ) Camera.WorldRotation = angles.WithYaw( -180 );

		Camera.WorldRotation = Camera.WorldRotation.Angles().WithRoll( 0 );

		if ( Input.Down( "forward" ) )
		{
			var dir = Camera.WorldRotation.Forward;
			Camera.WorldPosition += dir * 700 * Time.Delta;
		}

		if ( Input.Down( "left" ) )
		{
			var dir = Camera.WorldRotation.Left;
			Camera.WorldPosition += dir * 700 * Time.Delta;
		}

		if ( Input.Down( "right" ) )
		{
			var dir = Camera.WorldRotation.Right;
			Camera.WorldPosition += dir * 700 * Time.Delta;
		}

		if ( Input.Down( "backward" ) )
		{
			var dir = Camera.WorldRotation.Backward;
			Camera.WorldPosition += dir * 700 * Time.Delta;
		}
	}
}
