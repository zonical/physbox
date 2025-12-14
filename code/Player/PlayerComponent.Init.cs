using Sandbox;
using Sandbox.Diagnostics;

public partial class PlayerComponent
{
	protected override void OnStart()
	{
		// Shared init.
		Renderer = PlayerController.Renderer.GameObject;
		Collider = PlayerController.ColliderObject;

		if ( Network.IsOwner )
		{
			Log.Info( $"PlayerComponent::OnStart() - local init for {Name}" );
			Nametag.Name = Name;

			if ( IsPlayer )
			{
				LocalPlayer = this;

				CreateCamera();
				CreateViewmodel();

				FreeCam = false;
			}
			else if ( IsBot )
			{
				BotAgent.LinkEnter += OnBotLinkJump;
				PlayerController.Enabled = false;

				AssignBotName();

				// Make our agent move very quickly.
				BotAgent.Acceleration = PlayerConvars.RunSpeed;
				BotAgent.MaxSpeed = PlayerConvars.RunSpeed;
			}

			Scene.RunEvent<IPhysboxNetworkEvents>( x => x.OnPlayerInitialised( this ) );
		}

		if ( Networking.IsHost && IsPlayer )
		{
			Log.Info( $"PlayerComponent::OnStart() - host init for {Name}" );

			// If we are the only player, start the game.
			if ( Scene.GetAllComponents<PlayerComponent>().Count( x => x.IsPlayer ) == 1 )
			{
				var game = GameLogicComponent.GetGameInstance();
				game.StartGame();
			}
		}
	}

	private void CreateCamera()
	{
		Assert.NotNull( CameraPrefab );
		var camera = CameraPrefab.Clone();
		camera.Parent = GameObject;

		Camera = camera.GetComponent<CameraComponent>();
		Camera.Flags |= ComponentFlags.NotNetworked;

		Camera.Enabled = true;
		Camera.IsMainCamera = true;
		Camera.FieldOfView = Preferences.FieldOfView;
		PlayerController.UseCameraControls = true;
	}
}
