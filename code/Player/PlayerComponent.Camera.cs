using Physbox;
using System;
using System.Diagnostics;
using System.Threading.Channels;

public partial class PlayerComponent :
	BaseLifeComponent,
	IGameEvents,
	PlayerController.IEvents,
	Component.INetworkListener
{
	private bool _freeCam = false;
	private float _yaw = 0;
	private float _pitch = 0;
	public bool FreeCam
	{
		get { return _freeCam; }
		set
		{
			_freeCam = value;

			if ( !IsPlayer ) return;

			if ( _freeCam == true )
			{
				CreateFreeCam();
				Viewmodel?.Destroy();
				Viewmodel = null;
				PlayerController.Enabled = false;
			}
			else
			{
				CreateNormalCam();
				CreateViewmodel();
				PlayerController.Enabled = true;
			}

		}
	}

	private void InitaliseCamera()
	{
		if ( Camera is null )
		{
			var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/camera.prefab" );
			if ( prefab is null )
			{
				Log.Error( "Could not find prefab file." );
				return;
			}

			var prefabScene = SceneUtility.GetPrefabScene( prefab );
			var go = prefabScene.Clone( new(), name: "Camera" );
			go.BreakFromPrefab();
			go.NetworkMode = NetworkMode.Never;

			go.Parent = GameObject;

			Camera = go.GetComponent<CameraComponent>();
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
		float mouseX = -Input.MouseDelta.x * Time.Delta * (Preferences.Sensitivity * 3);
		float mouseY = Input.MouseDelta.y * Time.Delta * (Preferences.Sensitivity * 3);

		_yaw += mouseX;
		_pitch += mouseY;
		_pitch = MathX.Clamp( _pitch, -90f, 90f );

		Camera.LocalRotation = Rotation.From( new Angles( _pitch, _yaw, 0 ) );

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

	public void HandleCulling()
	{
		var time = new Stopwatch();
		time.Start();

		foreach ( var go in Scene.GetAllObjects(true) )
		{
			if ( go.Tags.Has( "ignore_culling" ) ) continue;

			// Disable all renderers if this object is not in the frustum.
			var enabled = Camera.GetFrustum().IsInside( go.WorldPosition );
			foreach ( var renderer in go.Components.GetAll<Renderer>(
				FindMode.EverythingInSelf | 
				FindMode.EverythingInChildren | 
				FindMode.EverythingInAncestors ) )
			{
				//renderer.Enabled = enabled;
			}
		}

		time.Stop();

		Log.Info( $"Time taken to cull: {time.ElapsedMilliseconds}ms" );
	}
}
