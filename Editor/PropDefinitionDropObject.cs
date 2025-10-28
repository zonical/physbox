using System.Threading;
using System.Threading.Tasks;
using Sandbox;
using Physbox;
using Editor;

[DropObject( "propdefinition", "pdef", "pdef_c" )]
partial class PropDefinitionDropObject : BaseDropObject
{
	GameResource Resource;

	protected override Task Initialize( string dragData, CancellationToken token )
	{
		Resource = InstallAsset( dragData, token ).Result.LoadResource<GameResource>();
		return Task.CompletedTask;
	}

	public override Task OnDrop()
	{
		// Create a prop in front of us.
		var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/breakable_prop.prefab" );
		if ( prefab is null )
		{
			Log.Error( "Could not find prefab file." );
			return Task.CompletedTask;
		}

		using var scene = SceneEditorSession.Scope();

		using ( SceneEditorSession.Active.UndoScope( "Drop Prop" ).WithGameObjectCreations().Push() )
		{
			GameObject = new GameObject( true, "Breakable Prop" );
			GameObject.WorldPosition = traceTransform.Position;
			GameObject.Tags.Add( PhysboxConstants.BreakablePropTag );

			// Create the necessary components.
			var modelRenderer = GameObject.AddComponent<ModelRenderer>();
			var modelCollider = GameObject.AddComponent<ModelCollider>();
			var rigidBody = GameObject.AddComponent<Rigidbody>();
			var life = GameObject.AddComponent<PropLifeComponent>();
			GameObject.AddComponent<ObjectCollisionListenerComponent>();
			
			// Set our resource (this gets converted to PropDefinitionResource on the game side).
			var defComp = GameObject.AddComponent<PropDefinitionComponent>();
			defComp.Definition = Resource;

			// Apply models.
			var model = Resource.GetSerialized().GetProperty( "Model" ).GetValue<Model>( Model.Error );
			modelRenderer.Model = model;
			modelCollider.Model = model;

			// Apply our mass.
			var mass = Resource.GetSerialized().GetProperty( "Mass" ).GetValue<float>();
			rigidBody.MassOverride = mass;

			// Apply our health.
			var maxHealth = Resource.GetSerialized().GetProperty( "MaxHealth" ).GetValue<int>();
			life.MaxHealth = maxHealth;

			// Update our name.
			var name = Resource.GetSerialized().GetProperty( "Name" ).GetValue<string>();
			GameObject.Name = $"Breakable Prop ({name})";
			GameObject.MakeNameUnique();
			GameObject.NetworkMode = NetworkMode.Object;
			GameObject.Network.SetOwnerTransfer( OwnerTransfer.Takeover );

			EditorScene.Selection.Clear();
			EditorScene.Selection.Add( GameObject );
		}

		return Task.CompletedTask;
	}
}
