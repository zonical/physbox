public partial class PlayerComponent :
	BaseLifeComponent,
	IGameEvents,
	PlayerController.IEvents,
	Component.INetworkListener
{
	private bool _freeCam;
	private float _pitch;
	private float _yaw;

	[Sync] public Frustum CameraFrustum { get; set; }

	public bool FreeCam
	{
		get => _freeCam;
		set
		{
			_freeCam = value;

			if ( !IsPlayer )
			{
				return;
			}

			if ( _freeCam )
			{
				CreateFreeCam();
				Viewmodel?.Enabled = false;

				PlayerController.Enabled = false;
			}
			else
			{
				CreateNormalCam();
				Viewmodel?.Enabled = true;

				PlayerController.Enabled = true;
			}
		}
	}

	/// <summary>
	/// Creates the scene camera, which is derived from a prefab that
	/// has some special components placed on top (mainly post-processing).
	/// </summary>
	private void CreateCamera()
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
			var go = prefabScene.Clone( new Transform(), name: "Camera" );
			go.BreakFromPrefab();
			go.NetworkMode = NetworkMode.Never;

			go.Parent = GameObject;

			Camera = go.GetComponent<CameraComponent>();
		}

		Camera.IsMainCamera = true;
		Camera.FieldOfView = Preferences.FieldOfView;
		Camera.BackgroundColor = Color.Black;

		// Create viewmodel.
		CreateViewmodel();

		FreeCam = false;
	}

	/// <summary>
	/// Detaches the camera from the player.
	/// </summary>
	private void CreateFreeCam()
	{
		if ( IsProxy )
		{
			return;
		}

		// Detach our camera from ourselves.
		Camera.GameObject.SetParent( null );
	}

	/// <summary>
	/// Creates the normal first-person camera.
	/// </summary>
	private void CreateNormalCam()
	{
		if ( IsProxy )
		{
			return;
		}

		// Reset our camera position.
		// 20/11/25 - Added + new Vector3( 0, 0, 64 ) after latest s&box update. Probably something
		// to do with the new play-mode and external game instance bullshit.
		Camera.GameObject.WorldPosition = GameObject.WorldPosition + new Vector3( 0, 0, 64 );
		Camera.GameObject.WorldRotation = GameObject.WorldRotation;
		Camera.GameObject.SetParent( GameObject );
	}

	/// <summary>
	/// Freecam movement.
	/// </summary>
	private void HandleNoclipMovement()
	{
		var mouseX = Input.AnalogLook.yaw;
		var mouseY = Input.AnalogLook.pitch;

		_yaw += mouseX;
		_pitch += mouseY;
		_pitch = _pitch.Clamp( -90f, 90f );

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
}
