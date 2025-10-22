using Sandbox;
using Sandbox.Audio;
using Sandbox.ModelEditor.Nodes;
using System;
using System.Threading.Tasks;

[Group( "Physbox" )]
[Title( "Prop Health" )]
[Icon( "favorite" )]
[Tint( EditorTint.Yellow )]
public sealed class PropLifeComponent : BaseLifeComponent
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

	[Sync] private PropDefinitionResource _def { get; set; }

	[Sync] public PlayerComponent LastOwnedBy { get; set; }
	[Property]
	public PropDefinitionResource Definition
	{
		get { return _def; }
		set
		{
			if ( _def == value )
			{
				return;
			}

			// Refresh model.
			_def = value;
			if ( !GameObject.Flags.Contains( GameObjectFlags.Deserializing ) )
			{
				ApplyResourceToProp();
			}
		}
	}

	public void ApplyResourceToProp()
	{
		if ( IsProxy ) return;
		if ( Definition is null || !Definition.IsValid ) return;

		PropRenderer.Model = Definition.Model;
		Collider.Model = Definition.Model;
		Rigidbody.MassOverride = Definition.Mass;
		MaxHealth = Definition.MaxHealth;
		Health = MaxHealth;
	}

	protected override void OnStart()
	{
		Spawn();
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

		// We need to add a delay here because the props are going so fast
		// that it's not registering the physics touch before the prop breaks.
		_ = CreateGibsWithDelay( GibCreationDelay );
	}

	[Rpc.Broadcast]
	public void PlayHitSoundToClient()
	{
		Sound.Play( "sounds/kenney/ui/minimize_003.vsnd_c", Mixer.FindMixerByName( "UI" ) );
	}

	private async Task CreateGibsWithDelay( float delay )
	{
		await Task.DelaySeconds( delay );

		// Shamelessly stolen and adapted from Prop component code.
		if ( PropRenderer?.Model is not null )
		{
			var breaklist = PropRenderer.Model.GetData<ModelBreakPiece[]>();
			if ( breaklist is not null && breaklist.Length > 0 )
			{
				foreach ( var breakModel in breaklist )
				{
					var model = Model.Load( breakModel.Model );
					if ( model is null || model.IsError )
						continue;

					var go = new GameObject( true, $"{GameObject.Name} (gib)" );

					var offset = breakModel.Offset;
					var placementOrigin = model.Attachments.GetTransform( "placementOrigin" );
					if ( placementOrigin.HasValue )
						offset = placementOrigin.Value.PointToLocal( offset );

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
		}
		DestroyGameObject();
	}
}
