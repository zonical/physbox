using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Sandbox;

// Because we don't have access to the MapInstance loader,
// we have to create our own. It sucks, I know.
public class PhysboxMapLoader( SceneMapInstanceComponent map, Vector3 origin )
	: SceneMapLoader( map.Scene.SceneWorld, map.Scene.PhysicsWorld, origin )
{
	protected override void CreateObject( ObjectEntry data )
	{
		base.CreateObject( data );

		switch ( data.TypeName )
		{
			case "env_sky":
				{
					var go = CreateNewGameObject( data );
					var component = go.Components.Create<SkyBox2D>();

					component.SkyMaterial = data.GetResource<Material>( "skyname" );
					component.Tint = data.GetValue<Color>( "tint_color" );
					component.SkyIndirectLighting = data.GetValue( "ibl", true );

					Log.Info( $"PhysboxMapLoader - Created 2D-Skybox ({component.SkyMaterial.ResourcePath})" );
					break;
				}
		}
	}

	private GameObject CreateNewGameObject( ObjectEntry kv )
	{
		var go = new GameObject();
		go.SetParent( map.MapContents );
		go.Flags |= GameObjectFlags.NotSaved;
		go.Name = string.IsNullOrWhiteSpace( kv.TargetName ) ? $"{kv.TypeName}" : $"{kv.TypeName} <{kv.TargetName}>";
		go.WorldTransform = kv.Transform;
		go.Tags.Add( kv.Tags );

		return go;
	}
}

[Group( "Physbox" )]
[Title( "Scene Map Instance" )]
[Icon( "map" )]
[Tint( EditorTint.Yellow )]
public class SceneMapInstanceComponent : Component, Component.ExecuteInEditor
{
	private const string ContentsGameObjectName = "Map Contents";

	[Property]
	[Title( "Map" )]
	[MapAssetPath]
	public string MapName { get; set; }

	private Map MapInstance { get; set; }
	private MapCollider Collider { get; set; }
	private SceneFile File { get; set; }
	public GameObject MapContents { get; private set; }

	protected override Task OnLoad()
	{
		return LoadMapAsync();
	}

	protected override void OnEnabled()
	{
		// If the component is moved, move everything else
		// in the scene along with it. The map contents (e.g. props)
		// should automatically move themselves since they are
		// parented to our GameObject.
		Transform.OnTransformChanged += () =>
		{
			if ( MapInstance is null )
			{
				return;
			}

			MapInstance.SceneMap.WorldOrigin = Transform.World.Position;
			foreach ( var body in MapInstance.PhysicsGroup.Bodies )
			{
				body.Transform = Transform.World;
			}
		};
	}

	protected override void OnDisabled()
	{
		MapContents?.Destroy();
		MapInstance?.Delete();
		Log.Info( $"Destroyed scenemap {MapName}" );
	}

	private async Task LoadMapAsync()
	{
		Cleanup();
		LoadingScreen.Title = $"Loading - {MapName}";

		// Load from cloud.
		File = await FetchMapFromCloud();
		if ( File is null )
		{
			return;
		}

		GameObject.Name = $"Map - {MapName}";

		var mapOrigin = GetMapOrigin( File );
		Log.Info( $"Loading scene map {File.ResourcePath} at {mapOrigin}" );

		// Load the actual map.
		var loader = new PhysboxMapLoader( this, mapOrigin );
		MapInstance = new Map( File.ResourcePath, loader );

		if ( Networking.IsHost || Scene.IsEditor )
		{
			AddSceneGameObjects();
		}

		// Assigns all the physics bodies a collider and the GameObject
		// attached to this component so we can actually process
		// collisions properly.
		AddCollision();

		Log.Info( $"Loaded scene map {MapName}" );
	}

	private async Task<SceneFile> FetchMapFromCloud()
	{
		var package = await Package.Fetch( MapName, false );
		if ( package?.Revision == null )
		{
			Log.Error( $"Could not find package {MapName}" );
			return null;
		}

		await package.MountAsync();

		var primaryAsset = package.GetMeta( "PrimaryAsset", "" );
		return await ResourceLibrary.LoadAsync<SceneFile>( primaryAsset );
	}

	private void Cleanup()
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		// Delete existing stuff in the map.
		var existingContents = MapContents ?? Scene.Directory.FindByName( ContentsGameObjectName ).FirstOrDefault();
		existingContents?.Destroy();

		// Destroy map collider if it already exists.
		if ( Components.TryGet<MapCollider>( out var collider ) )
		{
			collider.Destroy();
		}
	}

	private void AddCollision()
	{
		// Create map collider.
		Collider = Components.Create<MapCollider>();

		// Loop over the bodies and shapes that we've created and assign our collider to them.
		foreach ( var body in MapInstance.PhysicsGroup.Bodies )
		{
			body.GameObject = GameObject;
			foreach ( var shape in body.Shapes )
			{
				shape.Collider = Collider;
			}
		}
	}

	private void AddSceneGameObjects()
	{
		foreach ( var jsonGo in File.GameObjects )
		{
			// If the scene contains a map instance component, don't load it.
			// We've already created the map above ourselves.
			if ( ObjectContainsMapInstance( jsonGo ) )
			{
				continue;
			}

			var go = new GameObject { Parent = GameObject, Flags = GameObjectFlags.NotSaved };
			go.Deserialize( jsonGo );
		}
	}

	private static bool ObjectContainsMapInstance( JsonObject jsonGo )
	{
		var comps = jsonGo["Components"].AsArray();
		return comps.Select( x => x.AsObject() ).Any( comp => comp["__type"].ToString() == "Sandbox.MapInstance" );
	}

	private static Vector3 GetMapOrigin( SceneFile file )
	{
		foreach ( var jsonGo in file.GameObjects )
		{
			var comps = jsonGo["Components"]?.AsArray();
			foreach ( var comp in comps.Select( x => x.AsObject() ) )
			{
				if ( comp["__type"]?.ToString() == "Sandbox.MapInstance" )
				{
					return Vector3.Parse( jsonGo["Position"]?.ToString() );
				}
			}
		}

		return Vector3.Zero;
	}

	private static string GetMapName( SceneFile file )
	{
		foreach ( var jsonGo in file.GameObjects )
		{
			var comps = jsonGo["Components"]?.AsArray();
			foreach ( var comp in comps.Select( x => x.AsObject() ) )
			{
				if ( comp["__type"]?.ToString() == "Sandbox.MapInstance" )
				{
					return comp["MapName"]?.ToString();
				}
			}
		}

		return "";
	}
}
