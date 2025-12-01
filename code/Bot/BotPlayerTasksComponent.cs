using Sandbox;
using Sandbox.Citizen;
using Sandbox.Navigation;
using System.Linq;

public partial class BotPlayerTasksComponent : Component
{
	[RequireComponent] public PlayerComponent Player { get; set; }
	[RequireComponent] public NavMeshAgent Agent { get; set; }
	[RequireComponent] public CitizenAnimationHelper AnimationHelper { get; set; }

	// ==================== [ PROP SEARCHING ] ====================

	[Property]
	[Feature( "Prop Searching" )]
	[ReadOnly]
	public bool IsHoldingSomething => Player.HeldGameObject is not null;

	[Property]
	[Feature( "Prop Searching" )]
	public GameObject InterestedProp { get; set; }

	[Property]
	[Feature( "Prop Searching" )]
	public int MaximumPickupAttempts { get; set; } = 5;

	[Property]
	[Feature( "Prop Searching" )]
	public int PickupAttemptsRemaining { get; set; } = 5;

	[Property]
	[Feature( "Prop Searching" )]
	public int PropSearchRadius { get; set; } = 256;

	[Property]
	[Feature( "Prop Searching" )]
	public int PickupRadius { get; set; } = 64;

	// ==================== [ PLAYER SEARCHING ] ====================

	[Property]
	[Feature( "Player Searching" )]
	public GameObject InterestedPlayer { get; set; }

	[Property]
	[Feature( "Player Searching" )]
	public int MaximumThrowAttempts { get; set; } = 5;

	[Property]
	[Feature( "Player Searching" )]
	public int ThrowAttemptsRemaining { get; set; } = 5;

	[Property]
	[Feature( "Player Searching" )]
	public int PlayerSearchRadius { get; set; } = 512;

	[Property]
	[Feature( "Player Searching" )]
	public int PlayerThrowRadius { get; set; } = 128;

	private TimeSince TimeSinceLastAllocation { get; set; } = new();
	private Vector3 EyePositon => WorldPosition + new Vector3( 0, 0, 72 );
	private Vector3 LastMovePoint = Vector3.Zero;
	private Color DebugColor;
	private string DebugCurrentTask;


	[ConVar( "pb_debug_visualise_bots", ConVarFlags.Cheat )]
	public static bool DebugVisualiseBots { get; set; } = false;

	protected override void OnEnabled()
	{
		TimeSinceLastAllocation = 0;
		DebugColor = Color.Random;
	}

	protected override void OnUpdate()
	{
		AnimationHelper.WithVelocity( Agent.Velocity );
		AnimationHelper.WithWishVelocity( Agent.WishVelocity );

		if ( IsProxy )
		{
			return;
		}

		if ( !Player.IsAlive )
		{
			return;
		}

		UpdateRotation();


		if ( IsHoldingSomething &&
		     InterestedPlayer is not null &&
		     Vector3.DistanceBetween( WorldPosition, InterestedPlayer.WorldPosition ) < PlayerThrowRadius &&
		     Player.BuiltUpForce.AlmostEqual( Player.MaxForce ) )
		{
			// Throw!
			Player.ThrowHeldObject();

			InterestedPlayer = null;
			InterestedProp = null;
			ThrowAttemptsRemaining = MaximumThrowAttempts;
		}

		if ( TimeSinceLastAllocation.Relative > 1.5 )
		{
			var movePoint = (Vector3?)null;

			// Go try to find a prop.
			if ( Player.CanPickupObjects && !IsHoldingSomething )
			{
				FindPropTask( ref movePoint );
			}

			if ( IsHoldingSomething )
			{
				FindNearestPlayerTask( ref movePoint );
			}

			// If we haven't found anywhere to move yet, just find
			// a random spot on the NavMesh, and we can try again later.
			if ( movePoint is null )
			{
				DebugCurrentTask = "MovingToRandomSpot";
				movePoint = Scene.NavMesh.GetRandomPoint();
			}

			DebugRender( movePoint );

			if ( movePoint is not null )
			{
				Agent.MoveTo( (Vector3)movePoint );
				LastMovePoint = movePoint ?? Vector3.Zero;
			}

			TimeSinceLastAllocation = 0;
		}

		var textPos = new Vector3( WorldPosition.x, WorldPosition.y, WorldPosition.z + 96 );
		DebugOverlay.Text( textPos,
			new TextRendering.Scope( $"Task: {DebugCurrentTask}", DebugColor, 64 ),
			TextFlag.Left, 1.5f );
	}

	private void DebugRender( Vector3? movePoint )
	{
		if ( !DebugVisualiseBots )
		{
			return;
		}

		if ( movePoint is null )
		{
			return;
		}

		var eyePos = new Vector3( WorldPosition.x, WorldPosition.y, WorldPosition.z + 64 );
		var movePointAdjusted =
			new Vector3( (float)movePoint?.x, (float)movePoint?.y, (float)movePoint?.z + 64 );
		var debugpoints = new List<Vector3> { eyePos, movePointAdjusted };
		DebugOverlay.Line( debugpoints, DebugColor, 1.5f );
		DebugOverlay.Model( Model.Load( "models/citizen/citizen.vmdl" ),
			DebugColor.WithAlpha( 0.5f ), 1.5f, new Transform( (Vector3)movePoint, WorldRotation ) );
	}

	private void UpdateRotation()
	{
		var pointOfInterest = InterestedPlayer is null
			? LastMovePoint
			: InterestedPlayer.WorldPosition + GetPlayerVelocity( InterestedPlayer );
		var dir = (pointOfInterest - EyePositon).Normal;
		var rot = Rotation.From( dir.EulerAngles.WithPitch( 0 ) );

		WorldRotation = InterestedPlayer is null
			? Rotation.Slerp( WorldRotation, rot, Time.Delta * 3f )
			: Rotation.Lerp( WorldRotation, rot, Time.Delta * 3f );
	}

	private void FindPropTask( ref Vector3? movePoint )
	{
		// We do not have a prop, so lets go and find one!
		if ( !IsHoldingSomething && InterestedProp is null )
		{
			DebugCurrentTask = "FindingProp";
			var nearestProp = FindNearestProp( PropSearchRadius );
			// If there is another bot already interested in this prop,
			// don't also go for it. This prevents a sea of bots swarming
			// one prop and getting stuck on each other.
			var prop = nearestProp?.GetComponent<PropLifeComponent>();
			if ( prop is { InterestedBot: null } )
			{
				prop.InterestedBot = Player;
				InterestedProp = nearestProp;
				movePoint = InterestedProp.WorldPosition;
			}
		}

		// We are now interested in something. Let's move towards it.
		if ( !IsHoldingSomething && InterestedProp is not null )
		{
			DebugCurrentTask = "MovingToProp";
			PickupAttemptsRemaining--;

			// We've tried too many times to move towards this object.
			// Let's go find another one next time.
			if ( PickupAttemptsRemaining <= 0 )
			{
				var prop = InterestedProp?.GetComponent<PropLifeComponent>();
				prop?.InterestedBot = null;

				InterestedProp = null;
				PickupAttemptsRemaining = MaximumPickupAttempts;
			}
			else
			{
				movePoint = InterestedProp.WorldPosition;
			}
		}

		// If we are within the pickup radius of our interested prop.
		// Let's pick it up!
		if ( InterestedProp is not null && IsInPickupRadius( InterestedProp ) )
		{
			Player.PickupObject( InterestedProp );
			PickupAttemptsRemaining = MaximumPickupAttempts;
		}
	}

	private void FindNearestPlayerTask( ref Vector3? movePoint )
	{
		if ( IsHoldingSomething )
		{
			DebugCurrentTask = "FindingPlayer";
			// Find a player for us to throw this at.
			var target = FindNearestPlayer( PlayerSearchRadius );
			if ( target is not null )
			{
				InterestedPlayer = target;
			}
		}

		// We are now interested in something. Let's move towards it.
		if ( IsHoldingSomething && InterestedPlayer is not null )
		{
			DebugCurrentTask = "MovingToPlayer";
			ThrowAttemptsRemaining--;

			// We've tried too many times to move towards this object.
			// Let's go find another one next time.
			if ( ThrowAttemptsRemaining <= 0 )
			{
				InterestedPlayer = null;
				ThrowAttemptsRemaining = MaximumThrowAttempts;
			}
			else
			{
				movePoint = InterestedPlayer.WorldPosition;
			}
		}
	}

	private GameObject? FindNearestProp( int radius )
	{
		var trace = Scene.Trace.Sphere( radius, new Ray( WorldPosition, Vector3.Zero ), 1 )
			.WithTag( PhysboxConstants.BreakablePropTag )
			.WithoutTags( PhysboxConstants.HeldPropTag )
			.IgnoreGameObject( GameObject )
			.Run();

		return trace.GameObject;
	}

	private GameObject? FindNearestPlayer( int radius )
	{
		var traces = Scene.Trace.Sphere( radius, new Ray( WorldPosition, Vector3.Zero ), 1 )
			.WithTag( PhysboxConstants.PlayerTag )
			.IgnoreGameObject( GameObject )
			.RunAll();

		foreach ( var trace in traces )
		{
			if ( trace.GameObject is null )
			{
				continue;
			}

			if ( trace.GameObject.Components.TryGet<PlayerComponent>(
				    out var foundPlayer,
				    FindMode.EverythingInSelf |
				    FindMode.EverythingInDescendants |
				    FindMode.EverythingInAncestors ) )
			{
				// Ignore dead players. They still technically exist in the world.
				if ( !foundPlayer.IsAlive )
				{
					continue;
				}

				// Don't look for our own teammates.
				if ( foundPlayer.Team == Player.Team )
				{
					continue;
				}

				// Return the first player we get.
				return foundPlayer.GameObject;
			}
		}

		return null;
	}

	private bool IsInPickupRadius( GameObject prop )
	{
		var traces = Scene.Trace.Sphere( PickupRadius, new Ray( WorldPosition, Vector3.Zero ), 1 )
			.WithTag( PhysboxConstants.BreakablePropTag )
			.RunAll();

		foreach ( var trace in traces )
		{
			if ( trace.GameObject == prop )
			{
				return true;
			}
		}

		return false;
	}

	private Vector3 GetPlayerVelocity( GameObject go )
	{
		if ( go.Components.TryGet<PlayerController>( out var player ) )
		{
			return player.Velocity;
		}

		return Vector3.Zero;
	}
}
