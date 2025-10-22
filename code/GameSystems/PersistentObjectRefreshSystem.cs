using Sandbox;

// Any props that are manually placed within a map/scene will be respawned
// after a round ends.
public struct PersistantProp
{
	public PropDefinitionResource PropDef;
	public Transform Transform;
}

public struct PersistantMesh
{
	public PolygonMesh Mesh;
	public string Name;
	public Transform Transform;
	public int MaxHealth;
}

public class PersistentObjectRefreshSystem : GameObjectSystem, IGameEvents, ISceneLoadingEvents
{
	public List<PersistantProp> PersistantProps = new();
	public List<PersistantMesh> PersistantMeshes = new();

	public PersistentObjectRefreshSystem( Scene scene ) : base( scene )
	{

	}

	public void AfterLoad( Scene scene )
	{
		if ( Scene.IsEditor ) return;
		if ( Connection.Local != Connection.Host ) return;

		var sceneInfo = Scene.Get<SceneInformation>();
		var sceneName = sceneInfo is not null ? sceneInfo.Title : scene.Name;

		Log.Info( $"PersistentObjectRefreshSystem - loaded scene: {sceneName}." );
		PersistantProps.Clear();
		PersistantMeshes.Clear();

		// Create a list of all props.
		foreach ( var prop in scene.GetAllComponents<PropLifeComponent>() )
		{
			if ( prop.Definition is null || !prop.Definition.IsValid ) continue;
			PersistantProps.Add( new() { PropDef = prop.Definition, Transform = prop.WorldTransform } );

			// Delete this prop.
			prop.DestroyGameObject();
		}

		// Create a list of all meshes.
		foreach ( var mesh in scene.GetAllComponents<MeshComponent>() )
		{
			if ( mesh.Components.TryGet<WorldLifeComponent>( out var life ) )
			{
				PersistantMeshes.Add( new()
				{
					Mesh = mesh.Mesh,
					Transform = mesh.WorldTransform,
					Name = life.Name,
					MaxHealth = life.MaxHealth
				} );

				// Delete this mesh.
				mesh.DestroyGameObject();
			}
		}

		Log.Info( $"PersistentObjectRefreshSystem - stored {PersistantProps.Count} props." );
		Log.Info( $"PersistentObjectRefreshSystem - stored {PersistantMeshes.Count} meshes." );
	}

	[Rpc.Host( NetFlags.HostOnly | NetFlags.SendImmediate )]
	void IGameEvents.OnRoundStart()
	{
		RespawnProps();
		RespawnMeshes();
	}

	private void RespawnProps()
	{
		// Create a prop in front of us.
		var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/breakable_prop.prefab" );
		if ( prefab is null )
		{
			Log.Error( "Could not find prefab file." );
			return;
		}

		var spawnedProps = 0;
		// Respawn all props.
		foreach ( var storedProp in PersistantProps )
		{
			var prefabScene = SceneUtility.GetPrefabScene( prefab );
			var go = prefabScene.Clone();
			go.BreakFromPrefab();

			if ( go.Components.TryGet<PropLifeComponent>( out var prop ) )
			{
				prop.Definition = storedProp.PropDef;
				prop.ApplyResourceToProp();

				go.WorldPosition = storedProp.Transform.Position;
				go.WorldRotation = storedProp.Transform.Rotation;
				go.WorldScale = storedProp.Transform.Scale;
				go.NetworkSpawn();
				spawnedProps++;
			}
			else
			{
				Log.Error( "Failed to make prop, prefab does not contain PropLifeComponent." );
				go.Destroy();
			}
		}

		Log.Info( $"PersistentObjectRefreshSystem - spawned {spawnedProps} persisted props." );
	}

	private void RespawnMeshes()
	{
		var spawnedMeshes = 0;

		// Respawn all props.
		foreach ( var storedMesh in PersistantMeshes )
		{
			var go = new GameObject( true, storedMesh.Name );

			var mesh = go.AddComponent<MeshComponent>();
			mesh.Mesh = storedMesh.Mesh;
			mesh.RebuildMesh();

			var life = go.AddComponent<WorldLifeComponent>();
			life.MaxHealth = storedMesh.MaxHealth;
			life.Health = life.MaxHealth;
			life.Name = storedMesh.Name;

			go.AddComponent<ObjectCollisionListenerComponent>();

			go.WorldTransform = storedMesh.Transform;

			go.NetworkSpawn();
			spawnedMeshes++;
		}

		Log.Info( $"PersistentObjectRefreshSystem - spawned {spawnedMeshes} persisted meshes." );
	}
}
