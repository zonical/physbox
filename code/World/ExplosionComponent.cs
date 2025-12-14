using System.Reflection;
using System.Threading.Tasks;
using Physbox;

namespace Sandbox;

public class ExplosionComponent : Component
{
	[Property] public bool DealDamage { get; set; } = true;
	[Property] public int BaseDamage { get; set; } = 75;
	[Property] public float Radius { get; set; } = 256;
	public PlayerComponent Owner;
	public PropDefinitionResource Prop;

	[ActionGraphIgnore]
	private void PerformExplosionTrace()
	{
		if ( Owner is null )
		{
			return;
		}

		var traces = Scene.Trace.Sphere( Radius, new Ray( WorldPosition, Vector3.Up ), 1 )
			.IgnoreStatic()
			.WithAnyTags( PhysboxConstants.BreakablePropTag, PhysboxConstants.PlayerTag )
			.RunAll();

		foreach ( var trace in traces )
		{
			var go = trace.GameObject;
			if ( go is null )
			{
				continue;
			}

			if ( go.Components.TryGet<BaseLifeComponent>( out var life,
				    FindMode.Enabled | FindMode.InSelf | FindMode.InAncestors | FindMode.InDescendants ) )
			{
				var distance = trace.Distance;
				var distanceDamage = 100 * (1 - distance / Radius);
				distanceDamage = float.Round( float.Max( 0, distanceDamage ) );

				var victim = life as PlayerComponent;
				if ( victim is null )
				{
					continue;
				}

				if ( GameLogicComponent.UseTeams &&
				     !GameLogicComponent.FriendlyFire &&
				     victim.Team == Owner.Team &&
				     victim != Owner )
				{
					continue;
				}

				life.RequestDamage( new PhysboxDamageInfo
				{
					Victim = victim, Damage = (int)distanceDamage, Attacker = Owner, Prop = Prop
				} );
			}
		}
	}

	[Rpc.Broadcast]
	public void BroadcastSound()
	{
		Sound.Play( "sounds/effects/explosion/explosion_small.sound", WorldPosition );
	}

	[ActionGraphNode( "physbox.create_explosion" )]
	[Title( "Create Explosion" )]
	[Group( "Physbox" )]
	[Icon( "warning" )]
	public static ExplosionComponent CreateExplosion( PropLifeComponent propObject )
	{
		if ( propObject is null )
		{
			return null;
		}

		var scene = Game.ActiveScene;
		using ( scene.Push() )
		{
			// Create effect. (This should be loaded with CloudAssetWarmup).
			var prefab = ResourceLibrary.Get<PrefabFile>( "particles/explosion/explosion.medium.prefab" );
			if ( prefab is null )
			{
				Log.Error( "Could not find prefab file for ExplosionComponent." );
				return null;
			}

			var prefabScene = SceneUtility.GetPrefabScene( prefab );
			var go = prefabScene.Clone( new Transform(), name: "Explosion Effect" );
			go.BreakFromPrefab();
			go.WorldPosition = propObject.WorldPosition;

			var temp = go.AddComponent<TemporaryEffect>();
			temp.DestroyAfterSeconds = 3.0f;
			var explosion = go.AddComponent<ExplosionComponent>();

			go.NetworkMode = NetworkMode.Object;
			go.NetworkSpawn();

			explosion.Owner = propObject.GetComponentInParent<PlayerComponent>() ?? propObject.LastOwnedBy;
			explosion.Prop = propObject.PropDefinition;
			explosion.PerformExplosionTrace();
			explosion.BroadcastSound();

			return explosion;
		}
	}
}
