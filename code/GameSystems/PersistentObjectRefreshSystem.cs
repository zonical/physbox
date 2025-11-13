using Sandbox;
using Physbox;

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
		if ( PhysboxUtilites.IsMainMenuScene() ) return;
		if ( Connection.Local != Connection.Host ) return;

		PersistantProps.Clear();
		PersistantMeshes.Clear();

		// Create a list of all props.
		foreach ( var prop in scene.GetAllComponents<PropDefinitionComponent>() )
		{
			if ( prop.Definition is null || !prop.Definition.IsValid ) continue;
			PersistantProps.Add( new() 
			{ 
				PropDef = prop.Definition as PropDefinitionResource, 
				Transform = prop.WorldTransform 
			} );

			// Delete this prop. We'll spawn it in later.
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

				// Delete this mesh. We'll spawn it in later.
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
		var spawnedProps = 0;

		// Respawn all props.
		foreach ( var storedProp in PersistantProps )
		{
			var go = PhysboxUtilites.CreatePropFromResource( storedProp.PropDef );
			go.WorldPosition = storedProp.Transform.Position;
			go.WorldRotation = storedProp.Transform.Rotation;
			go.WorldScale = storedProp.Transform.Scale;
			spawnedProps++;
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
