using Physbox;
using Sandbox;
using Sandbox.Diagnostics;
using System;

public class PropSpawnerSystem : GameObjectSystem
{
	[ConVar( "pb_spawner_max_props", ConVarFlags.Replicated ),
		Title( "Maximum Number of Props" ), Group( "Props" )]
	public static int MaximumProps { get; set; } = 100;

	[ConVar( "pb_spawner_delay", ConVarFlags.Replicated ),
		Title( "Delay Between Prop Spawns" ), Group( "Props" )]
	public static int Delay { get; set; } = 3;

	public TimeSince SpawnDelay { get; set; }
	public TimeSince CheckDelay { get; set; }

	private List<PropSpawnerComponent> ValidSpawners = new();

	public PropSpawnerSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 10, LoopThroughSleepingSpawners, "LoopThroughSleepingSpawners" );
		Listen( Stage.StartUpdate, 11, SpawnProp, "SpawnProp" );

		SpawnDelay = 0;
		CheckDelay = 0;
	}

	void SpawnProp()
	{
		if ( Scene.IsEditor ) return;
		if ( !ValidSpawners.Any() ) return;
		if ( SpawnDelay.Relative < Delay ) return;

		var props = Scene.GetAllComponents<PropLifeComponent>().Where( p => p.Tags.Contains( PhysboxConstants.BreakablePropTag ) );
		if ( props.Count() >= MaximumProps ) return;

		// Spawn from a random spawner.
		var spawner = Random.Shared.FromList( ValidSpawners );
		spawner.Sleeping = true;
		spawner.TimeSinceWentToSleep = 0;

		// Create a prop in front of us.
		var go = PhysboxUtilites.CreatePropFromResource( spawner.Prop );
		go.WorldPosition = spawner.WorldPosition;
		go.WorldRotation = spawner.WorldRotation;

		SpawnDelay = 0;
	}

	void LoopThroughSleepingSpawners()
	{
		if ( Scene.IsEditor ) return;
		if ( CheckDelay.Relative < 1 ) return;

		using ( Performance.Scope( "PropSpawnerSystem.LoopThroughSleepingSpawners()" ) )
		{
			ValidSpawners.Clear();

			var spawners = Scene.GetAllComponents<PropSpawnerComponent>().Where( x => x.TimeSinceWentToSleep.Relative >= 2 );
			if ( !spawners.Any() ) return;

			// Loop through all spawners and see if they have space to spawn. If so, mark them
			// as valid.
			foreach ( var spawner in spawners )
			{
				if ( spawner.HasSpaceToSpawn() )
				{
					ValidSpawners.Add( spawner );
				}
			}
		}
	}

	[ConCmd( "pb_spawner_print_props" )]
	public static void DebugPrintProps()
	{
		var props = Game.ActiveScene.GetAllComponents<ModelRenderer>().Where( p => p.Tags.Contains( PhysboxConstants.BreakablePropTag ) );
		Log.Info( $"Total props: {props.Count()}" );

		var dict = new Dictionary<string, int>();
		var modelNames = props.Select( p => p.Model.Name );
		foreach ( var name in modelNames )
		{
			if ( !dict.ContainsKey( name ) )
			{
				dict.Add( name, 1 );
				continue;
			}
			dict[name]++;
		}

		Log.Info( $"Prop breakdown:" );
		foreach ( var (name, count) in dict )
		{
			Log.Info( $"	\"{name}\" - {count}" );
		}
	}
}
