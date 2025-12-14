using System;
using Physbox;
using Sandbox.Citizen;
using Sandbox.Diagnostics;
using Sandbox.Movement;

[Group( "Physbox" )]
[Title( "Physbox Player" )]
[Icon( "directions_run" )]
[Tint( EditorTint.Yellow )]
public partial class PlayerComponent :
	BaseLifeComponent,
	IPhysboxGameEvents,
	PlayerController.IEvents
{
	// ==================== [ LOCAL PLAYER INSTANCE ] ====================
	public static PlayerComponent LocalPlayer { get; private set; }

	// ==================== [ COMPONENTS ] ====================
	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	[ShowIf( "IsBot", false )]
	public PlayerController PlayerController => Components.Get<PlayerController>( true );

	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	[ShowIf( "IsBot", false )]
	public Voice Voice => Components.Get<Voice>( true );

	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	[ShowIf( "IsBot", false )]
	public CameraComponent Camera { get; set; } // This is manually set in InitaliseCamera().

	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	[ShowIf( "IsBot", true )]
	public NavMeshAgent BotAgent => Components.Get<NavMeshAgent>( true );

	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	private Nametag Nametag => Components.GetInChildren<Nametag>( true );

	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	[ShowIf( "IsBot", false )]
	private HudRoot Hud => Components.Get<HudRoot>( true );

	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	[ShowIf( "IsBot", false )]
	private Killfeed Killfeed => Components.Get<Killfeed>( true );

	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	[ShowIf( "IsBot", false )]
	public Chat Chat => Components.Get<Chat>( true );

	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	[ShowIf( "IsBot", false )]
	public PauseMenu PauseMenu => Components.Get<PauseMenu>( true );

	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	[ShowIf( "IsBot", false )]
	public Dresser Dresser => Components.Get<Dresser>( true );

	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	[ShowIf( "IsBot", false )]
	public ScreenPanel ScreenPanel => Components.Get<ScreenPanel>( true );

	[Property]
	[ReadOnly]
	[Feature( "Components" )]
	public Rigidbody Rigidbody => Components.Get<Rigidbody>( true );

	// ==================== [ PROPERTIES ] ====================
	[Sync] [Property] public int Kills { get; set; } = 0;
	[Sync] [Property] public int Deaths { get; set; } = 0;
	[Sync] public bool GodMode { get; set; } = false;
	[Sync] private bool HasDied { get; set; } = false;
	[Property] public Vector3 HitboxSize { get; set; } = new();
	[Property] public Vector3 HitboxOffset { get; set; } = new();

	[Property]
	[Sync( SyncFlags.FromHost )]
	public bool IsBot { get; set; }

	public bool IsPlayer => !IsBot;

	[Property]
	[Sync( SyncFlags.FromHost )]
	[ShowIf( "IsBot", true )]
	public string BotName { get; set; }

	public string Name => IsPlayer ? Network.Owner.DisplayName : BotName;
	[Property] [Feature( "Team" )] public Team Team { get; set; } = Team.None;

	// ==================== [ GAME OBJECTS ] ====================
	[Property]
	[ReadOnly]
	[Feature( "Game Objects" )]
	private GameObject Ragdoll { get; set; }

	[Property]
	[ReadOnly]
	[Feature( "Game Objects" )]
	public GameObject Viewmodel { get; set; }

	[Property]
	[ReadOnly]
	[Feature( "Game Objects" )]
	public GameObject Renderer { get; set; }

	[Property]
	[ReadOnly]
	[Feature( "Game Objects" )]
	public GameObject Collider { get; set; }

	public SkinnedModelRenderer RendererComponent => Renderer.GetComponent<SkinnedModelRenderer>();

	// ==========================================================

	/// <summary>
	/// Updates the viewmodel to play a jump animation.
	/// </summary>
	void PlayerController.IEvents.OnJumped()
	{
		TriggerViewmodelJump();
	}


	/// <summary>
	/// Deals fall damage to a player. I couldn't put this in ObjectCollisionListenerComponent,
	/// so it's here instead :(
	/// </summary>
	/// <param name="distance">Distance travelled.</param>
	/// <param name="impactVelocity">Impact velocity from the fall.</param>
	void PlayerController.IEvents.OnLanded( float distance, Vector3 impactVelocity )
	{
		if ( distance < 200 )
		{
			return;
		}

		if ( !(impactVelocity.Length >= ObjectCollisionProcessorSystem.FallSpeedThreshold) )
		{
			return;
		}

		var other = PlayerController.GroundObject;
		var listener = GetComponent<ObjectCollisionListenerComponent>();
		if ( listener is not null )
		{
			if ( listener.RecentlyHitBy.Contains( other.Id ) )
			{
				return;
			}

			// Don't register collisions with the thing we are touching for a while.
			listener.RecentlyHitBy.Add( other.Id );
			Invoke( 1.0f, () => listener.RecentlyHitBy.Remove( other.Id ) );
		}

		var collisionProcessor = Scene.GetSystem<ObjectCollisionProcessorSystem>();
		collisionProcessor.RegisterCollisionEvent( GameObject, other, impactVelocity.Length );
	}

	/// <summary>
	/// Reset thrower-related things on round start.
	/// </summary>
	void IPhysboxGameEvents.OnRoundStart()
	{
		if ( IsProxy )
		{
			return;
		}

		CanPickupObjects = true;
		HeldGameObject = null;
		CurrentlyLookingAtObject = null;
		BuiltUpForce = 0;
	}

	/// <summary>
	/// Disable our ability to pickup objects when the round ends.
	/// </summary>
	void IPhysboxGameEvents.OnRoundEnd()
	{
		if ( IsProxy )
		{
			return;
		}

		CanPickupObjects = false;
		if ( HeldGameObject is not null )
		{
			DropObject();
		}
	}

	/// <summary>
	/// Enables the spectator camera and moves the camera to a specator spawnpoint.
	/// </summary>
	private void InitSpectator()
	{
		FreeCam = true;
		HidePlayer();

		var spectatorSpawnpoint = Game.Random.FromList( Scene.GetAllComponents<PhysboxSpectatorSpawnpoint>().ToList() );
		if ( spectatorSpawnpoint is not null )
		{
			Camera.WorldPosition = spectatorSpawnpoint.WorldPosition;
			Camera.WorldRotation = spectatorSpawnpoint.WorldRotation;
		}
		else
		{
			Log.Warning( "Spectator spawnpoint not found! Reverting to regular spawnpoints." );
			var regularSpawnpoint = Game.Random.FromList( Scene.GetAllComponents<PhysboxSpawnpoint>().ToList() );
			if ( regularSpawnpoint is null )
			{
				return;
			}

			Camera.WorldPosition = regularSpawnpoint.WorldPosition;
			Camera.WorldRotation = regularSpawnpoint.WorldRotation;
		}
	}

	/// <summary>
	/// Main update loop.
	/// </summary>
	protected override void OnUpdate()
	{
		if ( IsProxy )
		{
			// Update bot animations
			if ( IsBot )
			{
				var animHelper = Components.Get<CitizenAnimationHelper>();
				if ( animHelper is null )
				{
					return;
				}

				animHelper.WithVelocity( NetworkedBotVelocity );
				animHelper.WithWishVelocity( NetworkedBotWishVelocity );
			}
		}
		else
		{
			// If our held object suddenly gets destroyed for whatever reason,
			// we should stop owning it.
			if ( HeldGameObject?.IsDestroyed ?? false )
			{
				FreeAndReturnHeldObject();
			}

			if ( IsPlayer )
			{
				OnPlayerUpdate();
			}

			if ( IsBot )
			{
				OnBotUpdate();
			}
		}
	}

	/// <summary>
	/// Main player update loop.
	/// </summary>
	private void OnPlayerUpdate()
	{
		// There's probably a better way to implement this, but this will do for now.
		if ( Camera is not null )
		{
			CameraFrustum = Camera.GetFrustum();
		}

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

	protected override void OnFixedUpdate()
	{
		// Contrary to OnUpdate() above, we can let bots build up force.
		if ( IsProxy )
		{
			return;
		}

		// If we are holding down the attack button, charge up the force.
		if ( HeldGameObject is not null )
		{
			BuiltUpForce = float.Min( BuiltUpForce + ForcePerFrame, MaxForce );
		}
	}

	/// <summary>
	/// Applies local clothing to the player model.
	/// </summary>
	private void DressPlayer()
	{
		if ( IsProxy )
		{
			return;
		}

		// If we already have clothes on, get rid of them.
		foreach ( var x in Components.GetAll<ModelRenderer>( FindMode.InDescendants )
			         .Where( y => y.Tags.Contains( "clothing" ) ) )
		{
			x.DestroyGameObject();
		}

		Dresser.BodyTarget = RendererComponent;
		Dresser.Source = Dresser.ClothingSource.Manual;
		Dresser.ApplyHeightScale = false;

		var localClothing = IsPlayer ? ClothingContainer.CreateFromLocalUser() : new ClothingContainer();

		AttemptToApplyBotClothing( localClothing );
		AttemptToApplyTeamClothing( localClothing );
		ApplyAndSpawnClothing( localClothing );

		// I'm probably doing something stupid in my code that makes it so that
		// the team tints aren't applying automatically on the first spawn for bots.
		// To get around this, I've added a short delay here which will always
		// apply the tint.
		if ( GameLogicComponent.UseTeams )
		{
			Invoke( 0.1f, ApplyTeamTintToClothes );
		}
	}

	private void AttemptToApplyBotClothing( ClothingContainer container )
	{
		if ( !IsBot )
		{
			return;
		}

		var jumpsuit =
			ResourceLibrary.Get<Clothing>( "models/citizen_clothes/shirt/jumpsuit/blue_jumpsuit.clothing" );
		if ( jumpsuit is not null )
		{
			container.Add( jumpsuit );
		}
	}

	private void AttemptToApplyTeamClothing( ClothingContainer container )
	{
		// If we are using teams, apply a shirt overtop of our usual stuff.
		if ( !GameLogicComponent.UseTeams )
		{
			return;
		}

		var shirt = ResourceLibrary.Get<Clothing>(
			"models/citizen_clothes/jumper/loose_jumper/loose_jumper.clothing" );

		if ( shirt is not null )
		{
			container.Add( shirt );
		}

		var pants = ResourceLibrary.Get<Clothing>(
			"models/citizen_clothes/trousers/trackiebottoms/trackie_bottoms.clothing" );

		if ( pants is not null )
		{
			container.Add( pants );
		}
	}

	private void ApplyAndSpawnClothing( ClothingContainer container )
	{
		Dresser.Clothing = container.Clothing;
		_ = Dresser.Apply();
	}

	private void ApplyTeamTintToClothes()
	{
		foreach ( var modelRen in Components.GetAll<ModelRenderer>( FindMode.InDescendants )
			         .Where( x => x.Tags.Contains( "clothing" ) ) )
		{
			// Apply team colour tint.
			if ( modelRen.Model.Name.Contains( "loose_jumper" ) ||
			     modelRen.Model.Name.Contains( "trackie_bottoms" ) )
			{
				modelRen.Tint = PhysboxUtilities.GetTeamColor( Team );
			}
		}
	}

	/// <summary>
	/// Creates a ragdoll at the player's location.
	/// </summary>
	private void CreateRagdoll()
	{
		if ( IsProxy )
		{
			return;
		}

		Ragdoll = PlayerController.CreateRagdoll();
		var tempEffect = Ragdoll.AddComponent<TemporaryEffect>();
		tempEffect.DestroyAfterSeconds = PlayerConvars.RespawnTime;
		Ragdoll.Tags.Add( PhysboxConstants.RagdollTag, PhysboxConstants.DebrisTag );
		Ragdoll.NetworkSpawn( Network.Owner );
	}

	protected override void DrawGizmos()
	{
		if ( Gizmo.IsSelected || Gizmo.IsHovered )
		{
			var box = BBox.FromPositionAndSize( HitboxOffset, HitboxSize );
			Gizmo.Draw.LineThickness = 1f;
			Gizmo.Draw.Color = Gizmo.Colors.Red.WithAlpha( Gizmo.IsSelected ? 1f : 0.2f );
			Gizmo.Draw.LineBBox( in box );
		}
	}

	/// <summary>
	/// Called when a player spawns.
	/// </summary>
	private void MovePlayerToSpawnpoint()
	{
		var spawnpoint = (GameObject)null;

		// Prioritise Physbox spawnpoints first, then find a normal Sandbox one.
		spawnpoint = Game.Random.FromList( Scene.GetAllComponents<PhysboxSpawnpoint>()
			             .Where( x => x.IsValidSpawnPoint( this ) ).ToList() )?.GameObject
		             ?? Game.Random.FromList( Scene.GetAllComponents<SpawnPoint>().ToList() )?.GameObject;

		if ( spawnpoint is null )
		{
			return;
		}

		// Teleport to spawnpoint.
		WorldPosition = spawnpoint.WorldPosition;
		PlayerController.EyeAngles = spawnpoint.WorldRotation;

		// If we are a bot, force set our destination.
		if ( IsBot && Components.TryGet<BotPlayerTasksComponent>( out var bot ) )
		{
			bot.Agent.SetAgentPosition( WorldPosition );
		}
	}

	/// <summary>
	/// Custom use function that ignores the hitbox.
	/// </summary>
	private void HandleUseInput()
	{
		if ( Input.Pressed( "use" ) )
		{
			// Find objects in front of to use!
			var ray = new Ray( Camera.WorldPosition, Camera.WorldRotation.Forward );
			var trace = Scene.Trace.Ray( ray, 256 )
				.IgnoreGameObject( GameObject )
				.WithoutTags(
					PhysboxConstants.BreakablePropTag,
					PhysboxConstants.RagdollTag,
					PhysboxConstants.DebrisTag )
				.Run();

			if ( trace.GameObject is null )
			{
				return;
			}

			var pressEvent = new IPressable.Event( this, ray );

			// Find all pressables and... use them!
			foreach ( var pressable in trace.GameObject.Components.GetAll<IPressable>(
				         FindMode.EnabledInSelf |
				         FindMode.InDescendants |
				         FindMode.InAncestors ) )
			{
				if ( !pressable.CanPress( pressEvent ) )
				{
					continue;
				}

				var success = pressable.Press( pressEvent );

				var viewmodel = Viewmodel.GetComponent<SkinnedModelRenderer>();
				viewmodel.Parameters.Set( "b_attack", true );

				if ( success )
				{
					pressable.Release( pressEvent );
				}
			}
		}
	}

	/// <summary>
	/// I personally like hiding all the components away on the player GameObject
	/// so I can easily debug things going on with PlayerComponent.
	/// </summary>
	private void HideDeveloperComponents()
	{
		Hud.Flags = ComponentFlags.Hidden | ComponentFlags.NotEditable;
		Killfeed.Flags = ComponentFlags.Hidden | ComponentFlags.NotEditable;
		Chat.Flags = ComponentFlags.Hidden | ComponentFlags.NotEditable;
		PauseMenu.Flags = ComponentFlags.Hidden | ComponentFlags.NotEditable;
		Dresser.Flags = ComponentFlags.Hidden | ComponentFlags.NotEditable;
		Voice.Flags = ComponentFlags.Hidden | ComponentFlags.NotEditable;
		ScreenPanel.Flags = ComponentFlags.Hidden | ComponentFlags.NotEditable;
		PlayerController.Flags = ComponentFlags.Hidden | ComponentFlags.NotEditable;
		Rigidbody.Flags = ComponentFlags.Hidden | ComponentFlags.NotEditable;

		foreach ( var moveMode in Components.GetAll<MoveMode>() )
		{
			moveMode.Flags = ComponentFlags.Hidden | ComponentFlags.NotEditable;
		}
	}

	/// <summary>
	/// But if you really want to see all the components, I won't stop you.
	/// </summary>
	[Icon( "star" )]
	[Button]
	[Feature( "Components" )]
	[Title( "Fine. Show me all Components." )]
	private void ShowDeveloperComponents()
	{
		Hud?.Flags &= ~(ComponentFlags.Hidden | ComponentFlags.NotEditable);
		Killfeed?.Flags &= ~(ComponentFlags.Hidden | ComponentFlags.NotEditable);
		Chat?.Flags &= ~(ComponentFlags.Hidden | ComponentFlags.NotEditable);
		PauseMenu?.Flags &= ~(ComponentFlags.Hidden | ComponentFlags.NotEditable);
		Dresser?.Flags &= ~(ComponentFlags.Hidden | ComponentFlags.NotEditable);

		Voice?.Flags &= ~(ComponentFlags.Hidden | ComponentFlags.NotEditable);
		ScreenPanel?.Flags &= ~(ComponentFlags.Hidden | ComponentFlags.NotEditable);
		PlayerController?.Flags &= ~(ComponentFlags.Hidden | ComponentFlags.NotEditable);
		Rigidbody?.Flags &= ~(ComponentFlags.Hidden | ComponentFlags.NotEditable);

		foreach ( var moveMode in Components.GetAll<MoveMode>() )
		{
			moveMode.Flags &= ~(ComponentFlags.Hidden | ComponentFlags.NotEditable);
		}
	}

	/// <summary>
	/// Plays a hitsound to the player.
	/// </summary>
	[Rpc.Owner]
	public void PlayHitsound()
	{
		if ( IsBot )
		{
			return;
		}

		Sound.Play( "sounds/player/ding-hitsound.sound" );
	}

	/// <summary>
	/// Plays the fall damage "bone breaking" sound to the player.
	/// </summary>
	/// <param name="position"></param>
	[Rpc.Broadcast]
	public void PlayFallDamageSound( Vector3 position )
	{
		Sound.Play( "sounds/player/bone-break.sound", position );
	}

	/// <summary>
	/// Assigns the player to the next best team.
	/// </summary>
	public void AssignToBestTeam()
	{
		if ( !GameLogicComponent.UseTeams )
		{
			return;
		}

		var teamCounts = GameLogicComponent.GetGameInstance().AvaliableTeams.ToDictionary( team => team, team => 0 );
		foreach ( var player in Scene.GetAllComponents<PlayerComponent>() )
		{
			if ( player.Team == Team.None )
			{
				continue;
			}

			teamCounts[player.Team]++;
		}

		Team = teamCounts.MinBy( x => x.Value ).Key;
	}
}
