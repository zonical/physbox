using Physbox;
using System;
using System.Linq;
using System.Threading;

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
	public PlayerController PlayerController => Components.GetOrCreate<PlayerController>();
	public Voice Voice => Components.GetOrCreate<Voice>();
	public CameraComponent Camera { get; set; } // This is manually set in InitaliseCamera().
	private Nametag Nametag => Components.GetInChildren<Nametag>( true );
	private HudRoot Hud => Components.Get<HudRoot>( true );

	// ==================== [ PROPERTIES ] ====================
	[Sync, Property, ReadOnly] public int Kills { get; set; } = 0;
	[Sync, Property, ReadOnly] public int Deaths { get; set; } = 0;
	[Sync] public bool GodMode { get; set; } = false;
	[Sync] private bool HasDied { get; set; } = false;
	[Property] public Vector3 HitboxSize { get; set; } = new();
	[Property] public Vector3 HitboxOffset { get; set; } = new();

	// ==================== [ GAME OBJECTS ] ====================

	[Property, ReadOnly] private GameObject Ragdoll;
	[Property, Sync, ReadOnly] private GameObject Hitbox { get; set; }

	// ==========================================================

	[Rpc.Owner]
	public void InitPlayer()
	{
		Nametag.Name = Network.Owner.DisplayName;
		LocalPlayer = this;

		// Dress the player.
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

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

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

			if ( Hitbox is not null )
			{
				Hitbox.WorldPosition = WorldPosition;
				if ( PlayerConvars.DrawPlayerHitboxes )
				{
					DebugOverlay.Box( Hitbox.GetBounds(), Color.Red );
				}
			}
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		// If we are holding down the attack button, charge up the force.
		if ( HeldGameObject is not null )
		{
			BuiltUpForce = float.Min( BuiltUpForce + ForcePerFrame, MaxForce );
		}
	}

	public override void OnDamage( in DamageInfo damage )
	{
		if ( IsProxy ) return;
		if ( GodMode ) return;

		base.OnDamage( damage );
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
		Hitbox = new GameObject( $"Player - {Network.Owner.DisplayName} (hitbox)" );
		Hitbox.Tags.Add( PhysboxConstants.BreakableOnlyTag );

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

	[ActionGraphNode( "physbox.get_local_player" )]
	[Title( "Get Local Player" ), Group( "Physbox" ), Icon( "home" )]
	public static PlayerComponent GetGameInstance()
	{
		return LocalPlayer;
	}
}
