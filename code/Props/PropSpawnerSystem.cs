using Physbox;
using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Threading.Tasks;

public class PropSpawnerSystem : GameObjectSystem, IPhysboxGameEvents, ISceneLoadingEvents
{
	[ConVar( "pb_spawner_max_props", ConVarFlags.Replicated )]
	[Title( "Maximum Number of Props" )]
	[Group( "Props" )]
	public static int MaximumProps { get; set; } = 100;

	[ConVar( "pb_spawner_delay", ConVarFlags.Replicated )]
	[Title( "Delay Between Prop Spawns" )]
	[Group( "Props" )]
	public static int Delay { get; set; } = 3;

	public TimeSince SpawnDelay { get; set; }
	public TimeSince CheckDelay { get; set; }

	// Spawners that can be selected from. This is filled at the
	// beginning of each round so we can exclude certain spawners
	// e.g. different spawners for different gamemodes.
	private List<PropSpawnerComponent> AvaliableSpawners = new();

	// Spawners that we are allowed to spawn from. This is
	// filled dynamically.
	private List<PropSpawnerComponent> ValidSpawners = new();
	private int SpawnerCount = 0;

	public PropSpawnerSystem( Scene scene ) : base( scene )
	{
		if ( PhysboxUtilities.IsMainMenuScene() || !Networking.IsHost )
		{
			return;
		}

		Listen( Stage.StartUpdate, 10, LoopThroughSleepingSpawners, "LoopThroughSleepingSpawners" );
		Listen( Stage.StartUpdate, 11, SpawnProp, "SpawnProp" );

		SpawnDelay = 0;
		CheckDelay = 0;
	}

	void ISceneLoadingEvents.AfterLoad( Scene scene )
	{
		SpawnerCount = scene.GetAllComponents<PropSpawnerComponent>().Count();
	}

	void IPhysboxGameEvents.OnRoundStart()
	{
		if ( Scene.IsEditor || !Networking.IsHost )
		{
			return;
		}

		AvaliableSpawners = Scene.GetAllComponents<PropSpawnerComponent>()
			.Where( SpawnerIsValidForGameMode ).ToList();

		// If we have any spawners marked to spawn their props immediately, spawn them here.
		foreach ( var spawner in AvaliableSpawners )
		{
			if ( !spawner.SpawnImmediately )
			{
				continue;
			}

			spawner.Sleeping = true;
			spawner.TimeSinceWentToSleep = 0;

			PhysboxUtilities.CreatePropFromResource( spawner.Prop, spawner.WorldTransform );
		}
	}

	private bool SpawnerIsValidForGameMode( PropSpawnerComponent spawner )
	{
		return spawner.AnyGameMode || spawner.GameModes.Contains( GameLogicComponent.GameMode );
	}

	private void SpawnProp()
	{
		if ( Scene.IsEditor || PhysboxUtilities.IsMainMenuScene() || !ValidSpawners.Any() || !Networking.IsHost )
		{
			return;
		}

		var props = Scene.GetAllComponents<PropLifeComponent>()
			.Where( p => p.Tags.Contains( PhysboxConstants.BreakablePropTag ) );

		var maxProps = GameLogicComponent.GameMode != GameModes.Dodgeball ? MaximumProps : SpawnerCount;

		if ( props.Count() >= maxProps )
		{
			return;
		}

		// Spawn from a random spawner.
		var spawner = Random.Shared.FromList( ValidSpawners );
		spawner.Sleeping = true;
		spawner.TimeSinceWentToSleep = 0;

		// Create a prop in front of us.
		PhysboxUtilities.CreatePropFromResource( spawner.Prop, spawner.WorldTransform );

		SpawnDelay = 0;
	}

	private void LoopThroughSleepingSpawners()
	{
		if ( Scene.IsEditor || CheckDelay.Relative < 1 || PhysboxUtilities.IsMainMenuScene() || !Networking.IsHost )
		{
			return;
		}

		ValidSpawners.Clear();

		var spawners = AvaliableSpawners
			.Where( x => x.TimeSinceWentToSleep.Relative >= 2 ).ToList();
		if ( !spawners.Any() )
		{
			return;
		}

		// Loop through all spawners and see if they have space to spawn. If so, mark them
		// as valid.
		foreach ( var spawner in spawners.Where( spawner => spawner.HasSpaceToSpawn() ) )
		{
			ValidSpawners.Add( spawner );
		}
	}

	[ConCmd( "pb_spawner_print_props" )]
	public static void DebugPrintProps()
	{
		var props = Game.ActiveScene.GetAllComponents<ModelRenderer>()
			.Where( p => p.Tags.Contains( PhysboxConstants.BreakablePropTag ) );
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
