using System.Reflection;
using System.Threading.Tasks;

namespace Sandbox;

public class ExplosionComponent : Component
{
	[Property] public bool DealDamage { get; set; } = true;
	[Property] public int BaseDamage { get; set; } = 75;
	[Property] public float Radius { get; set; } = 256;
	public GameObject Owner;
	public GameObject Origin;

	[ActionGraphIgnore]
	void PerfomExplosionTrace()
	{
		var traces = Scene.Trace.Sphere( Radius, new Ray( WorldPosition, Vector3.Up ), 1 )
			.IgnoreStatic()
			.WithAnyTags( PhysboxConstants.BreakablePropTag, PhysboxConstants.PlayerTag )
			.RunAll();

		foreach ( var trace in traces )
		{
			var go = trace.GameObject;
			if ( go is null ) continue;

			if ( go.Components.TryGet<BaseLifeComponent>( out var life,
					FindMode.Enabled | FindMode.InSelf | FindMode.InAncestors | FindMode.InDescendants ) )
			{
				var distance = trace.Distance;
				var distanceDamage = 100 * (1 - distance / Radius);
				distanceDamage = float.Round( float.Max( 0, distanceDamage ) );

				life.RequestDamage( new()
				{
					Damage = distanceDamage,
					Attacker = Owner,
					Weapon = Origin
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
	[Title( "Create Explosion" ), Group( "Physbox" ), Icon( "warning" )]
	public static ExplosionComponent CreateExplosion( Vector3 position, GameObject owner, GameObject origin )
	{
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
			var go = prefabScene.Clone( new(), name: "Explosion Effect" );
			go.BreakFromPrefab();

			var temp = go.AddComponent<TemporaryEffect>();
			temp.DestroyAfterSeconds = 3.0f;

			var explosion = go.AddComponent<ExplosionComponent>();

			go.WorldPosition = position;

			go.NetworkMode = NetworkMode.Object;
			go.NetworkSpawn();

			explosion.Owner = owner;
			explosion.Origin = origin;
			explosion.PerfomExplosionTrace();
			explosion.BroadcastSound();

			return explosion;
		}
	}
}
