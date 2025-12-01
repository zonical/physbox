using System;
using System.Threading.Tasks;
using Physbox;
using Sandbox.ModelEditor.Nodes;

[Group( "Physbox" )]
[Title( "Prop Health" )]
[Icon( "favorite" )]
[Tint( EditorTint.Yellow )]
public sealed class PropLifeComponent :
	BaseLifeComponent, IPropDefinitionSubscriber, Component.INetworkVisible
{
	[ConVar( "pb_gib_creation_delay", ConVarFlags.Server,
		Help = "The delay between gibs being created after taking lethal damage. " +
		       "Higher numbers afford more accuracy when props hit players, but it may create a slightly delayed look!" )]
	public static float GibCreationDelay { get; set; } = 0.15f;

	[ConVar( "pb_prop_damage_immunity", ConVarFlags.Server,
		Help = "How long to wait before props are allowed to take damage." )]
	public static float PropDamageImmunityTime { get; set; } = 1.0f;

	public ModelRenderer PropRenderer => Components.Get<ModelRenderer>();
	public ModelCollider Collider => Components.Get<ModelCollider>();
	public Rigidbody Rigidbody => Components.Get<Rigidbody>();

	[Property]
	[Group( "Ownership" )]
	[Sync]
	public PlayerComponent LastOwnedBy { get; set; }

	public PlayerComponent InterestedBot { get; set; }

	public PropDefinitionComponent DefinitionComponent => Components.Get<PropDefinitionComponent>();
	public PropDefinitionResource PropDefinition => DefinitionComponent.Definition;

	public bool IsVisibleToConnection( Connection connection, in BBox worldBounds )
	{
		// Would love to look at this again in the future.
		return true;

		// If this prop is being held, always be networked.
		if ( Tags.Contains( PhysboxConstants.HeldPropTag ) )
		{
			return true;
		}

		// Get player owned by connection.
		var player = Scene
			.GetAll<PlayerComponent>()
			.FirstOrDefault( x => x.Network.Owner == connection );

		if ( player is null )
		{
			return false;
		}

		// If this prop is being held, or is being looked at by a player, network it.
		if ( player.HeldGameObject == GameObject ||
		     player.CurrentlyLookingAtObject == GameObject ||
		     player.LastHeldGameObject == GameObject )
		{
			return true;
		}

		return player.CameraFrustum.IsInside( worldBounds, true );
	}

	public void OnDefinitionChanged( PropDefinitionResource oldValue, PropDefinitionResource newValue )
	{
		// Update our prop.
		var resource = newValue;
		if ( resource is null )
		{
			return;
		}

		// Apply models.
		var model = resource.Model;
		PropRenderer.Model = model;
		Collider.Model = model;

		// Apply our mass.
		var mass = resource.Mass;
		Rigidbody.MassOverride = mass;

		// Apply our health.
		var maxHealth = resource.MaxHealth;
		MaxHealth = maxHealth;

		// Update our name.
		var name = resource.Name;
		GameObject.Name = $"Breakable Prop ({name})";
		GameObject.MakeNameUnique();
	}

	protected override void OnStart()
	{
		Spawn();
	}

	protected override void OnEnabled()
	{
		if ( IsProxy )
		{
			return;
		}

		GameObject.Network.AlwaysTransmit = true;
	}

	public override void Spawn()
	{
		base.Spawn();

		DamageImmunity = true;
		Invoke( PropDamageImmunityTime, () => { DamageImmunity = false; } );
	}

	public override void Die()
	{
		base.Die();

		// If we are being held, free ourselves from our owner.
		var owner = GetComponentInParent<PlayerComponent>();
		// This should ALWAYS return true, but it's here as a sanity check.
		if ( owner?.HeldGameObject == GameObject )
		{
			owner?.DropObject();
		}

		// Run death action.
		PropDefinition.OnPropBroken?.Invoke( GameObject );

		// We need to add a delay here because the props are going so fast
		// that it's not registering the physics touch before the prop breaks.
		_ = CreateGibsWithDelay( GibCreationDelay );
	}

	private async Task CreateGibsWithDelay( float delay )
	{
		// Don't create gibs in the main menu.
		if ( PhysboxUtilites.IsMainMenuScene() )
		{
			DestroyGameObject();
			return;
		}

		await Task.DelaySeconds( delay );

		// Shamelessly stolen and adapted from Prop component code.
		var breaklist = PropRenderer?.Model?.GetData<ModelBreakPiece[]>();
		if ( breaklist != null && breaklist.Length > 0 )
		{
			foreach ( var breakModel in breaklist )
			{
				var model = await Model.LoadAsync( breakModel.Model );
				if ( model is null || model.IsError )
				{
					continue;
				}

				var go = new GameObject( true, $"{GameObject.Name} (gib)" );

				var offset = breakModel.Offset;
				var placementOrigin = model.Attachments.GetTransform( "placementOrigin" );
				if ( placementOrigin.HasValue )
				{
					offset = placementOrigin.Value.PointToLocal( offset );
				}

				go.WorldPosition = WorldTransform.PointToWorld( offset );
				go.WorldRotation = WorldRotation;
				go.WorldScale = WorldScale;

				foreach ( var tag in breakModel.CollisionTags.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
				{
					go.Tags.Add( tag );
				}

				// Make sure we have the "debris" tag so this can't be picked up and reused.
				go.Tags.Add( PhysboxConstants.DebrisTag );

				var modelRen = go.Components.Create<ModelRenderer>();
				modelRen.Model = model;

				var modelPhys = go.Components.Create<Rigidbody>();
				modelPhys.Velocity = Rigidbody.Velocity;
				modelPhys.AngularVelocity = Rigidbody.AngularVelocity;

				var modelCollider = go.Components.Create<ModelCollider>();
				modelCollider.Model = model;

				var temp = go.AddComponent<TemporaryEffect>();
				temp.DestroyAfterSeconds = 3.0f;

				go.NetworkSpawn();
			}
		}

		DestroyGameObject();
	}
}
