using Physbox;
using System;
using Sandbox.Network;

[Group( "Physbox" )]
[Title( "Physbox Player" )]
[Icon( "directions_run" )]
[Tint( EditorTint.Yellow )]
public partial class PlayerComponent :
	BaseLifeComponent,
	IGameEvents,
	PlayerController.IEvents
{
	// ==================== [ LOCAL PLAYER INSTANCE ] ====================
	public static PlayerComponent LocalPlayer { get; private set; }

	// ==================== [ COMPONENTS ] ====================
	[Property, ReadOnly, Feature( "Components" ), ShowIf( "IsBot", false )]
	public PlayerController PlayerController => Components.GetOrCreate<PlayerController>();

	[Property, ReadOnly, Feature( "Components" ), ShowIf( "IsBot", false )]
	public Voice Voice => Components.Get<Voice>();

	[Property, ReadOnly, Feature( "Components" ), ShowIf( "IsBot", false )]
	public CameraComponent Camera { get; set; } // This is manually set in InitaliseCamera().

	[Property, ReadOnly, Feature( "Components" ), ShowIf( "IsBot", true )]
	public NavMeshAgent BotAgent => Components.Get<NavMeshAgent>();

	[Property, ReadOnly, Feature( "Components" )]
	private Nametag Nametag => Components.GetInChildren<Nametag>( true );

	[Property, ReadOnly, Feature( "Components" ), ShowIf( "IsBot", false )]
	private HudRoot Hud => Components.Get<HudRoot>( true );

	[Property, ReadOnly, Feature( "Components" ), ShowIf( "IsBot", false )]
	private Killfeed Killfeed => Components.Get<Killfeed>( true );

	// ==================== [ PROPERTIES ] ====================
	[Sync, Property, ReadOnly] public int Kills { get; set; } = 0;
	[Sync, Property, ReadOnly] public int Deaths { get; set; } = 0;
	[Sync] public bool GodMode { get; set; } = false;
	[Sync] private bool HasDied { get; set; } = false;
	[Property] public Vector3 HitboxSize { get; set; } = new();
	[Property] public Vector3 HitboxOffset { get; set; } = new();
	[Property, Sync( SyncFlags.FromHost )] public bool IsBot { get; set; }
	[Property, Sync( SyncFlags.FromHost ), ShowIf( "IsBot", true )] public string BotName { get; set; }

	// ==================== [ GAME OBJECTS ] ====================

	[Property, ReadOnly] private GameObject Ragdoll { get; set; }
	[Property, Sync, ReadOnly] private GameObject Hitbox { get; set; }
	[Property, ReadOnly] private GameObject Viewmodel { get; set; }

	// ==========================================================

	public bool IsPlayer => !IsBot;

	[Rpc.Owner]
	public void InitPlayer()
	{
		Nametag.Name = Network.Owner.DisplayName;
		LocalPlayer = this;

		DressPlayer();
		InitaliseCamera();
		HidePlayer();
		CreateHitbox();

		// Update speed variables based on ConVars.
		PlayerController.RunSpeed = PlayerConvars.RunSpeed;
		PlayerController.WalkSpeed = PlayerConvars.WalkSpeed;
		PlayerController.DuckedSpeed = PlayerConvars.DuckedSpeed;

		// Start in our spectator state.
		FreeCam = true;

		var spectatorSpawnpoint = Game.Random.FromList( Scene.GetAllComponents<PhysboxSpectatorSpawnpoint>().ToList() );
		if ( spectatorSpawnpoint is not null )
		{
			Camera.WorldPosition = spectatorSpawnpoint.WorldPosition + new Vector3( 0, 0, 64 ); // Add 64 units so we don't start in the ground.
			Camera.WorldRotation = spectatorSpawnpoint.WorldRotation;
		}
		else
		{
			Log.Warning( "Spectator spawnpoint not found! Reverting to regular spawnpoints." );
			var regularSpawnpoint = Game.Random.FromList( Scene.GetAllComponents<PhysboxSpawnpoint>().ToList() );
			if ( regularSpawnpoint is not null )
			{
				Camera.WorldPosition = regularSpawnpoint.WorldPosition;
				Camera.WorldRotation = regularSpawnpoint.WorldRotation;
			}
		}

		var game = GameLogicComponent.GetGameInstance();
		if ( !game.RoundOver )
		{
			RequestSpawn();
		}
	}

	[Rpc.Owner]
	public void InitBot()
	{
		var names = new List<string>()
		{
			"Adam",
			"Rhys",
			"Chloe",
			"Jasmine",
			"Jo",
			"Jack",
			"Maverick",
			"Ruth",
			"Paul",
			"Patrick",
			"Xavier",
			"Bridget",
			"Bianca",
			"Josh"
		};

		BotName = "[BOT] " + Game.Random.FromList( names );
		Nametag.Name = BotName;
		GameObject.Name = BotName;

		// Create a fake connection.
		Sandbox.Debug.Networking.AddEmptyConnection();

		// If we have the PlayerController component for what ever reason,
		// disable it. We only have one that we locally control.
		var playerController = GetComponent<PlayerController>();
		if ( playerController is not null )
		{
			playerController.Enabled = false;

			// Delete all of our existing colliders.
			foreach ( var collider in Components.GetAll<Collider>(
				FindMode.EverythingInSelf |
				FindMode.EverythingInAncestors |
				FindMode.EverythingInDescendants ) )
			{
				collider.Destroy();
			}
		}

		// Make our agent move very quickly.
		BotAgent.Acceleration = PlayerConvars.RunSpeed;
		BotAgent.MaxSpeed = PlayerConvars.RunSpeed;

		HidePlayer();
		CreateHitbox();

		var game = GameLogicComponent.GetGameInstance();
		if ( !game.RoundOver )
		{
			RequestSpawn();
		}
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		// Debug hitbox drawing.
		if ( Hitbox is not null )
		{
			Hitbox.WorldPosition = WorldPosition;
			if ( PlayerConvars.DrawPlayerHitboxes )
			{
				DebugOverlay.Box( Hitbox.GetBounds(), Color.Red );
			}
		}

		if ( IsPlayer ) OnPlayerUpdate();
		if ( IsBot ) OnBotUpdate();
	}

	private void OnPlayerUpdate()
	{
		if ( FreeCam )
		{
			HandleNoclipMovement();
			if ( IsAlive && HudRoot.DrawHud )
			{
				Camera.Hud.DrawText( "FREECAM ENABLED", 16, Color.Red, Screen.Size * 0.05 );
			}
		}

		if ( IsAlive && !FreeCam )
		{
			DrawCrosshair();
			HandleUseInput();
			UpdateViewmodel();

			if ( CanPickupObjects )
			{
				// If we're not holding anything, find a potential object and highlight it.
				if ( HeldGameObject is null )
				{
					FindPotentialTarget();
				}

				// Update the held object position.
				if ( HeldGameObject is not null )
				{
					PositionHeldObject();
					ThrowCheck();
					//PositionPreview();
				}

				HandleThrowerInput();
			}
		}
	}

	private void CreateViewmodel()
	{
		Viewmodel = new GameObject( true, "Viewmodel" );
		Viewmodel.NetworkMode = NetworkMode.Never;

		var modelComp = Viewmodel.AddComponent<SkinnedModelRenderer>();
		modelComp.Model = Cloud.Model( "facepunch.v_first_person_arms_human" );
		modelComp.RenderOptions.Overlay = true;
		modelComp.RenderOptions.Game = false;
		modelComp.RenderType = ModelRenderer.ShadowRenderType.Off;
		modelComp.UseAnimGraph = true;

		Viewmodel.WorldRotation = new Angles( 45, 0, 5 );
		
		// Parent model to camera.
		Viewmodel.Parent = Camera.GameObject;
	}

	private void UpdateViewmodel()
	{
		if ( Viewmodel is null ) return;

		Viewmodel.WorldPosition = Camera.WorldPosition - new Vector3(0, 0, 2) + Camera.WorldRotation.Forward * 8;
	}

	private void OnBotUpdate()
	{
		if ( !IsAlive ) return;

		if ( HeldGameObject is not null )
		{
			PositionHeldObject();
		}
	}

	protected override void OnFixedUpdate()
	{
		// Contrary to OnUpdate() above, we can let bots build up force.
		if ( IsProxy ) return;

		// If we are holding down the attack button, charge up the force.
		if ( HeldGameObject is not null )
		{
			BuiltUpForce = float.Min( BuiltUpForce + ForcePerFrame, MaxForce );
		}
	}

	void IGameEvents.OnRoundStart()
	{
		if ( IsProxy ) return;

		CanPickupObjects = true;
		HeldGameObject = null;
		CurrentlyLookingAtObject = null;
		BuiltUpForce = 0;
	}

	void IGameEvents.OnRoundEnd()
	{
		if ( IsProxy ) return;

		CanPickupObjects = false;
		if ( HeldGameObject is not null )
		{
			DropObject();
		}
	}

	private void DressPlayer()
	{
		var dresser = Components.Get<Dresser>();
		if ( dresser is null ) return;

		dresser.Clear();
		dresser.Apply();

		foreach ( var modelRen in Components.GetAll<ModelRenderer>( FindMode.InDescendants )
			.Where( x => x.Tags.Contains( "clothing" ) ) )
		{
			modelRen.GameObject.NetworkSpawn();
		}
	}

	private void CreateRagdoll()
	{
		if ( IsProxy ) return;

		Ragdoll = PlayerController.CreateRagdoll();
		Ragdoll.Tags.Add( PhysboxConstants.RagdollTag );
		Ragdoll.NetworkSpawn( Network.Owner );
	}

	/// <summary>
	/// The reason why a separate hitbox object is created is due to the way
	/// s&box handles tags on parented objects. When we parent a GameObject
	/// to another GameObject, the child inherits the tags of it's parent.
	/// This makes sense for most purposes, but when trying to create a custom
	/// hitbox for props to hit, having both tags "breakable_only" and "player"
	/// means that collisions don't work properly. This workaround creates a
	/// separate GameObject that is associated with the player, but not actually
	/// parented to it.
	/// </summary>
	private void CreateHitbox()
	{
		var hitboxname = IsPlayer ? $"Player - {Network.Owner.DisplayName} (hitbox)" : $"{BotName} (hitbox)";
		Hitbox = new GameObject( hitboxname );
		Hitbox.Tags.Add( PhysboxConstants.BreakableOnlyTag, PhysboxConstants.HitboxTag );

		var box = Hitbox.AddComponent<BoxCollider>();
		box.Scale = HitboxSize;
		box.Center = HitboxOffset;
		box.Elasticity = 0.25f;

		var collision = Hitbox.AddComponent<ObjectCollisionListenerComponent>();
		collision.CollisionProxy = GameObject;

		Hitbox.NetworkMode = NetworkMode.Object;
		Hitbox.Network.SetOrphanedMode( NetworkOrphaned.Destroy );
		Hitbox.NetworkSpawn();
	}

	protected override void DrawGizmos()
	{
		if ( Gizmo.IsSelected || Gizmo.IsHovered )
		{
			BBox box = BBox.FromPositionAndSize( HitboxOffset, HitboxSize );
			Gizmo.Draw.LineThickness = 1f;
			Gizmo.Draw.Color = Gizmo.Colors.Red.WithAlpha( Gizmo.IsSelected ? 1f : 0.2f );
			Gizmo.Draw.LineBBox( in box );
		}
	}

	private void HandleUseInput()
	{
		if ( Input.Pressed( "use" ) )
		{
			// Find objects in front of to use!
			var ray = new Ray( Camera.WorldPosition, Camera.WorldRotation.Forward );
			var trace = Scene.Trace.Ray( ray, 256 )
				.IgnoreGameObject( GameObject )
				.IgnoreGameObject( Hitbox )
				.WithoutTags(
					PhysboxConstants.BreakablePropTag,
					PhysboxConstants.RagdollTag,
					PhysboxConstants.DebrisTag )
				.Run();

			if ( trace.GameObject is not null )
			{
				var pressEvent = new IPressable.Event( this, ray );

				// Find all pressables and... use them!
				foreach ( var pressable in trace.GameObject.Components.GetAll<IPressable>(
					FindMode.EnabledInSelf |
					FindMode.InDescendants |
					FindMode.InAncestors ) )
				{
					if ( pressable.CanPress( pressEvent ) )
					{
						var success = pressable.Press( pressEvent );
						if ( success )
						{
							pressable.Release( pressEvent );
						}
					}
				}
			}
		}
	}

	[ActionGraphNode( "physbox.get_local_player" )]
	[Title( "Get Local Player" ), Group( "Physbox" ), Icon( "home" )]
	public static PlayerComponent GetGameInstance()
	{
		return LocalPlayer;
	}
}
