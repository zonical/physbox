using Sandbox;
using System;

public class CollisionEvent : IEquatable<CollisionEvent>
{
	public GameObject A;
	public GameObject B;

	public float AbsoluteSpeed;

	public CollisionEvent( GameObject _a, GameObject _b )
	{
		A = _a;
		B = _b;
	}

	public static bool operator ==( CollisionEvent A, CollisionEvent B )
	{
		if ( A is null )
		{
			return B is null;
		}

		return A.Equals( B );
	}

	public static bool operator !=( CollisionEvent A, CollisionEvent B )
	{
		if ( A is null )
		{
			return B is not null;
		}

		return !A.Equals( B );
	}

	public bool Equals( CollisionEvent? other )
	{
		if ( other == null )
			return false;

		return (this.A.Id == other.A.Id && this.B.Id == other.B.Id) ||
			(this.A.Id == other.B.Id && this.B.Id == other.A.Id);
	}

	public override bool Equals( object? obj ) => Equals( obj as CollisionEvent );

	public override int GetHashCode() => HashCode.Combine( A, B );

	public enum ObjectType
	{
		None = -1,
		World,
		Prop,
		Player
	}

	private FindMode _findMode = FindMode.Enabled | FindMode.InSelf | FindMode.InAncestors | FindMode.InChildren;

	private bool IsObjectProp( GameObject go )
	{
		return go.Components.TryGet<PropLifeComponent>( out _, _findMode );
	}

	private bool IsObjectWorld( GameObject go )
	{
		return go.Components.TryGet<MapCollider>( out _, _findMode ) ||
			go.Components.TryGet<MeshComponent>( out _, _findMode );
	}

	private bool IsObjectPlayer( GameObject go )
	{
		return go.Components.TryGet<PlayerComponent>( out _, _findMode );
	}

	public ObjectType GetObjectType( GameObject go )
	{
		if ( IsObjectWorld( go ) ) return ObjectType.World;
		if ( IsObjectProp( go ) ) return ObjectType.Prop;
		if ( IsObjectPlayer( go ) ) return ObjectType.Player;
		return ObjectType.None;
	}

	public Rigidbody GetRigidbody( GameObject go )
	{
		return go.Components.Get<Rigidbody>( _findMode );
	}

	public BaseLifeComponent GetLifeComponent( GameObject go )
	{
		return go.Components.Get<BaseLifeComponent>( _findMode );
	}
}

/// <summary>
/// ObjectCollisionListenerComponent's will listen out for collisions
/// (using the OnCollisionStart() callback) between GameObjects.
/// All of those collisions are processed in one centralised place.
/// The actual processing is only run on the host/server.
/// </summary>
public class ObjectCollisionProcessorSystem : GameObjectSystem
{
	[ConVar( "pb_prop_damage_speed_threshold", ConVarFlags.Server | ConVarFlags.Replicated,
	Help = "The minimum speed an object must be traveling to deal damage." )]
	public static float DamageSpeedThreshold { get; set; } = 250.0f;

	[ConVar( "pb_fall_damage_speed_threshold", ConVarFlags.Server | ConVarFlags.Replicated,
	Help = "The minimum speed a player must be traveling to receive fall damage." )]
	public static float FallSpeedThreshold { get; set; } = 500.0f;

	[ConVar( "pb_prop_always_damage_players", ConVarFlags.Server | ConVarFlags.Replicated,
	Help = "When set to true, props will always deal damage to players regardless of speed." )]
	public static bool PropsAlwaysDamagePlayers { get; set; } = true;

	[ConVar( "pb_debug_collision_iterations", ConVarFlags.Server,
	Help = "When set to true, logs the amount of collision interactions processed in an update." )]
	public static bool PrintCollisionInteractions { get; set; } = false;

	[ConVar( "pb_debug_verbose_collisions", ConVarFlags.Server,
	Help = "When set to true, logs every single collision." )]
	public static bool VerboseCollisionLogging { get; set; } = false;

	// List of collisions that need to be processed.
	private List<CollisionEvent> _collisions = new();

	public ObjectCollisionProcessorSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 10, ProcessCollisions, "ProcessCollisions" );
	}
	public void RegisterCollisionEvent( GameObject a, GameObject b, float speed )
	{
		var objectCollision = new CollisionEvent( a, b );
		objectCollision.AbsoluteSpeed = speed;

		if ( _collisions.Contains( objectCollision ) ) return;

		_collisions.Add( objectCollision );
	}

	public void ProcessCollisions()
	{
		//if ( Connection.Local != Connection.Host ) return;

		var iterations = 0;
		while ( _collisions.Any() )
		{
			var collision = _collisions.First();
			_collisions.Remove( collision ); // Pop from list.

			var objectTypeTuple = (collision.GetObjectType( collision.A ), collision.GetObjectType( collision.B ));

			// Anything marked with CollisionEvent.ObjectType.None will be ignored.
			if ( CollisionFunctions.ContainsKey( objectTypeTuple ) )
			{
				CollisionFunctions[objectTypeTuple].Invoke( collision, collision.A, collision.B );
				if ( VerboseCollisionLogging )
				{
					Log.Info( $"ObjectCollisionProcessorSystem - collision: {objectTypeTuple}" );
				}
			}

			iterations++;
		}

		if ( iterations > 0 && PrintCollisionInteractions )
		{
			Log.Info( $"ObjectCollisionProcessorSystem - Processed {iterations} in StartUpdate()." );
			iterations = 0;
		}
	}

	public Dictionary<(CollisionEvent.ObjectType, CollisionEvent.ObjectType), Action<CollisionEvent, GameObject, GameObject>> CollisionFunctions = new()
	{
		{ ( CollisionEvent.ObjectType.Prop, CollisionEvent.ObjectType.World ), (e, A, B) => { ProcessPropAndWorldCollision( e, A, B ); } },
		{ ( CollisionEvent.ObjectType.World, CollisionEvent.ObjectType.Prop ), (e, A, B) => { ProcessPropAndWorldCollision( e, B, A ); } },

		{ ( CollisionEvent.ObjectType.Player, CollisionEvent.ObjectType.World ), (e, A, B) => { ProcessPlayerAndWorldCollision( e, A, B ); } },
		{ ( CollisionEvent.ObjectType.World, CollisionEvent.ObjectType.Player ), (e, A, B) => { ProcessPlayerAndWorldCollision( e, B, A ); } },

		{ ( CollisionEvent.ObjectType.Player, CollisionEvent.ObjectType.Prop ), (e, A, B) => { ProcessPlayerAndPropCollision( e, A, B ); } },
		{ ( CollisionEvent.ObjectType.Prop, CollisionEvent.ObjectType.Player ), (e, A, B) => { ProcessPlayerAndPropCollision( e, B, A ); } },

		{ ( CollisionEvent.ObjectType.Prop, CollisionEvent.ObjectType.Prop ), (e, A, B) => { ProcessPropAndPropCollision( e, A, B ); } },
	};

	private static void ProcessPropAndWorldCollision( CollisionEvent @event, GameObject prop, GameObject world )
	{
		if ( @event.AbsoluteSpeed > DamageSpeedThreshold )
		{
			var damage = (int)float.Sqrt( @event.AbsoluteSpeed * 0.8f );

			var propLife = @event.GetLifeComponent( prop );
			if ( propLife is not null )
			{
				propLife.OnDamage( new DamageInfo( damage, world, null ) );
			}

			var worldLife = @event.GetLifeComponent( world );
			if ( worldLife is not null )
			{
				worldLife.OnDamage( new DamageInfo( damage, prop, null ) );
			}
		}
	}

	private static void ProcessPropAndPropCollision( CollisionEvent @event, GameObject propA, GameObject propB )
	{
		if ( @event.AbsoluteSpeed > DamageSpeedThreshold )
		{
			var lifeA = @event.GetLifeComponent( propA );
			if ( lifeA is not null )
			{
				var probB_RB = @event.GetRigidbody( propB );

				var damage = (int)float.Sqrt( @event.AbsoluteSpeed + probB_RB?.MassOverride ?? 0 );
				lifeA.OnDamage( new DamageInfo( damage, propB, null ) );
			}

			var lifeB = @event.GetLifeComponent( propB );
			if ( lifeB is not null )
			{
				var probA_RB = @event.GetRigidbody( propA );

				var damage = (int)float.Sqrt( @event.AbsoluteSpeed + probA_RB?.MassOverride ?? 0 );
				lifeB.OnDamage( new DamageInfo( damage, propA, null ) );
			}
		}
	}

	private static void ProcessPlayerAndWorldCollision( CollisionEvent @event, GameObject player, GameObject world )
	{
		if ( @event.AbsoluteSpeed >= FallSpeedThreshold )
		{
			var damage = (int)float.Sqrt( @event.AbsoluteSpeed );
			var life = @event.GetLifeComponent( player );

			if ( life is not null )
			{
				life.RequestDamage( new DamageInfo( damage, world, null ) );
			}
		}
	}

	private static void ProcessPlayerAndPropCollision( CollisionEvent @event, GameObject player, GameObject prop )
	{
		if ( @event.AbsoluteSpeed > DamageSpeedThreshold )
		{
			var propLife = @event.GetLifeComponent( prop ) as PropLifeComponent;
			var attacker = (GameObject)null;
			attacker = propLife.LastOwnedBy?.GameObject ?? propLife.GameObject;

			if ( propLife is not null )
			{
				var damage = (int)float.Sqrt( @event.AbsoluteSpeed );
				propLife.OnDamage( new DamageInfo( damage, null, null ) );

				// Bounce back.
				var rigidBody = @event.GetRigidbody( prop );
				if ( rigidBody is not null )
				{
					//rigidBody.Velocity = 0;
					//rigidBody.AngularVelocity = 0;
				}
			}

			var victim = @event.GetLifeComponent( player ) as PlayerComponent;
			if ( victim is not null )
			{
				var result = Game.ActiveScene.Trace.Ray( new Ray( prop.WorldPosition, Vector3.Down ), 64 )
						.WithoutTags(
							PhysboxConstants.PlayerTag,
							PhysboxConstants.DebrisTag,
							PhysboxConstants.RagdollTag )
						.Run();

				//Log.Info( $"{ result.GameObject}, {result.Distance}" );

				// Apply bonus damage if we are reasonably above the ground.
				var damage = (int)float.Sqrt( @event.AbsoluteSpeed ) + (result.Distance > 32 ? 10 : 0);

				var rigidBody = @event.GetRigidbody( prop );
				if ( rigidBody is not null )
				{
					damage += (int)float.Sqrt( rigidBody.MassOverride );
				}

				victim.RequestDamage( new DamageInfo( damage, attacker, prop ) );

				// Let the attacker know that we've hit the player by sending a hitsound.
				var attackerPlayer = attacker.GetComponent<PlayerComponent>();
				if ( attackerPlayer is not null )
				{
					attackerPlayer.PlayHitsound();

					// Print information in attacker chat.
					var chat = Game.ActiveScene.Get<ChatManagerComponent>();

					using ( Rpc.FilterInclude( c => c.Id == attackerPlayer.Network.Owner.Id ) )
					{
						var name = victim.IsPlayer ? victim.Network.Owner.DisplayName : victim.BotName;
						chat.SendMessage( MessageType.System, $"You dealt {damage} damage to {name}." );
					}
				}
			}
		}
	}
}
