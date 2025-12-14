using Sandbox;
using System;
using System.Threading;

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
		if ( other == null || A is null || B is null )
		{
			return false;
		}

		return (A.Id == other.A.Id && B.Id == other.B.Id) ||
		       (A.Id == other.B.Id && B.Id == other.A.Id);
	}

	public override bool Equals( object? obj )
	{
		return Equals( obj as CollisionEvent );
	}

	public override int GetHashCode()
	{
		return HashCode.Combine( A, B );
	}

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
		if ( IsObjectWorld( go ) )
		{
			return ObjectType.World;
		}

		if ( IsObjectProp( go ) )
		{
			return ObjectType.Prop;
		}

		if ( IsObjectPlayer( go ) )
		{
			return ObjectType.Player;
		}

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
	public static float DamageSpeedThreshold { get; set; } = 100.0f;

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
		Listen( Stage.PhysicsStep, 10, ProcessCollisions, "ProcessCollisions" );
	}

	public void RegisterCollisionEvent( GameObject a, GameObject b, float speed )
	{
		var objectCollision = new CollisionEvent( a, b );
		objectCollision.AbsoluteSpeed = speed;

		if ( _collisions.Contains( objectCollision ) )
		{
			return;
		}

		_collisions.Add( objectCollision );
	}

	private void ProcessCollisions()
	{
		if ( Scene.IsEditor || PhysboxUtilities.IsMainMenuScene() )
		{
			return;
		}

		// Don't process collisions and deal damage if the game has ended.
		if ( GameLogicComponent.GetGameInstance().RoundOver )
		{
			_collisions.Clear();
			return;
		}

		var iterations = 0;
		while ( _collisions.Count != 0 )
		{
			var collision = _collisions.First();
			_collisions.Remove( collision ); // Pop from list.

			var objectTypeTuple = (collision.GetObjectType( collision.A ), collision.GetObjectType( collision.B ));

			// Anything marked with CollisionEvent.ObjectType.None will be ignored.
			if ( CollisionFunctions.TryGetValue( objectTypeTuple, out var value ) )
			{
				value.Invoke( collision, collision.A, collision.B );
				if ( VerboseCollisionLogging )
				{
					Log.Info(
						$"ObjectCollisionProcessorSystem - event: ({objectTypeTuple.Item1}: {collision.A.Name}," +
						$" {objectTypeTuple.Item2}: {collision.B.Name})" );
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

	private readonly Dictionary<(CollisionEvent.ObjectType, CollisionEvent.ObjectType),
		Action<CollisionEvent, GameObject, GameObject>> CollisionFunctions = new()
	{
		{
			(CollisionEvent.ObjectType.Prop, CollisionEvent.ObjectType.World),
			( e, A, B ) => { ProcessPropAndWorldCollision( e, A, B ); }
		},
		{
			(CollisionEvent.ObjectType.World, CollisionEvent.ObjectType.Prop),
			( e, A, B ) => { ProcessPropAndWorldCollision( e, B, A ); }
		},
		{
			(CollisionEvent.ObjectType.Player, CollisionEvent.ObjectType.World),
			( e, A, B ) => { ProcessPlayerAndWorldCollision( e, A, B ); }
		},
		{
			(CollisionEvent.ObjectType.World, CollisionEvent.ObjectType.Player),
			( e, A, B ) => { ProcessPlayerAndWorldCollision( e, B, A ); }
		},
		{
			(CollisionEvent.ObjectType.Player, CollisionEvent.ObjectType.Prop),
			( e, A, B ) => { ProcessPlayerAndPropCollision( e, A, B ); }
		},
		{
			(CollisionEvent.ObjectType.Prop, CollisionEvent.ObjectType.Player),
			( e, A, B ) => { ProcessPlayerAndPropCollision( e, B, A ); }
		},
		{
			(CollisionEvent.ObjectType.Prop, CollisionEvent.ObjectType.Prop),
			( e, A, B ) => { ProcessPropAndPropCollision( e, A, B ); }
		}
	};

	private static void ProcessPropAndWorldCollision( CollisionEvent @event, GameObject prop, GameObject world )
	{
		if ( !(@event.AbsoluteSpeed > DamageSpeedThreshold) )
		{
			return;
		}

		var damage = (int)float.Sqrt( @event.AbsoluteSpeed * 0.8f );
		var propLife = @event.GetLifeComponent( prop ) as PropLifeComponent;

		if ( GameLogicComponent.GameMode == GameModes.Dodgeball )
		{
			// If we are playing Dodgeball, remove the person that owned us immediately.
			var owner = propLife?.LastOwnedBy;
			if ( owner is not null &&
			     owner.PropCancellationToken != CancellationToken.None &&
			     owner.PropCancellationToken.CanBeCanceled )
			{
				owner.PropCancellationTokenSource?.Cancel();
			}

			propLife?.LastOwnedBy = null;

			// Then get outta here. Don't deal any damage to props.
			return;
		}

		propLife?.OnDamage( new PhysboxDamageInfo { Prop = propLife?.PropDefinition, Damage = damage } );

		var worldLife = @event.GetLifeComponent( world ) as WorldLifeComponent;
		worldLife?.OnDamage( new PhysboxDamageInfo { Prop = propLife?.PropDefinition, Damage = damage } );
	}

	private static void ProcessPropAndPropCollision( CollisionEvent @event, GameObject propA, GameObject propB )
	{
		if ( !(@event.AbsoluteSpeed > DamageSpeedThreshold) )
		{
			return;
		}

		if ( @event.GetLifeComponent( propA ) is PropLifeComponent lifeA &&
		     @event.GetLifeComponent( propB ) is PropLifeComponent lifeB )
		{
			var propADef = lifeA.PropDefinition;
			var propBDef = lifeB.PropDefinition;

			// Prop A dealing damage to Prop B.
			var aRigidBody = @event.GetRigidbody( propA );
			var damageToB = (int)float.Sqrt( @event.AbsoluteSpeed + aRigidBody?.MassOverride ?? 0 );
			lifeB.OnDamage( new PhysboxDamageInfo { Damage = damageToB, Prop = propADef } );

			// Prop B dealing damage to Prop A.
			var bRigidBody = @event.GetRigidbody( propB );
			var damageToA = (int)float.Sqrt( @event.AbsoluteSpeed + bRigidBody?.MassOverride ?? 0 );
			lifeA.OnDamage( new PhysboxDamageInfo { Damage = damageToA, Prop = propBDef } );
		}
	}

	private static void ProcessPlayerAndWorldCollision( CollisionEvent @event, GameObject player, GameObject world )
	{
		if ( !(@event.AbsoluteSpeed >= FallSpeedThreshold) )
		{
			return;
		}

		var damage = float.Sqrt( @event.AbsoluteSpeed ) * 0.35f;

		if ( @event.GetLifeComponent( player ) is PlayerComponent life )
		{
			life.RequestDamage( new PhysboxDamageInfo { Victim = life, Prop = null, Damage = (int)damage } );
			life.PlayFallDamageSound( player.WorldPosition );
		}
	}

	private static void ProcessPlayerAndPropCollision( CollisionEvent @event, GameObject player, GameObject prop )
	{
		if ( !(@event.AbsoluteSpeed > DamageSpeedThreshold) )
		{
			return;
		}

		var propLife = @event.GetLifeComponent( prop ) as PropLifeComponent;
		var attacker = propLife?.LastOwnedBy?.GameObject;

		// Don't damage the player if we don't have someone who last owned us.
		// Prevent us from taking damage from idle props.
		if ( attacker is null )
		{
			return;
		}

		if ( @event.GetLifeComponent( player ) is PlayerComponent victim )
		{
			if ( victim.DamageImmunity )
			{
				return;
			}

			// Do not deal damage to our teammates if friendly fire is not enabled.
			if ( GameLogicComponent.UseTeams &&
			     !GameLogicComponent.FriendlyFire &&
			     attacker.GetComponent<PlayerComponent>().Team == victim.Team )
			{
				return;
			}

			// Deal damage to the player.
			var rigidBody = @event.GetRigidbody( prop );
			var playerDamage = 0;
			switch ( GameLogicComponent.GameMode )
			{
				// Insta-death. That's how dodgeball works, baby!
				case GameModes.Dodgeball:
					playerDamage = 100;
					break;

				// Normal damage.
				case GameModes.Deathmatch:
					playerDamage = (int)float.Sqrt( @event.AbsoluteSpeed + rigidBody?.MassOverride ?? 0 );
					break;
			}

			var attackerPlayer = attacker.GetComponent<PlayerComponent>();
			victim.RequestDamage( new PhysboxDamageInfo
			{
				Victim = victim, Attacker = attackerPlayer, Damage = playerDamage, Prop = propLife.PropDefinition
			} );

			// Deal damage to the prop.
			var propDamage = (int)float.Sqrt( @event.AbsoluteSpeed );
			propLife.OnDamage( new PhysboxDamageInfo { Damage = propDamage } );

			// Let the attacker know that we've hit the player by sending a hitsound.
			if ( attackerPlayer is not null && attackerPlayer.IsPlayer )
			{
				attackerPlayer.PlayHitsound();

				// Print information in attacker chat.
				var name = victim.IsPlayer ? victim.Network.Owner.DisplayName : victim.BotName;
				PhysboxUtilities.SendMessageToOnlyConnection( attackerPlayer.Network.Owner, MessageType.System,
					$"You dealt {playerDamage} damage to {name}." );
			}
		}
	}
}
