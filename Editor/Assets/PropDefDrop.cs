using System.Threading;
using System.Threading.Tasks;
using Editor;

[DropObject( "propdefinition", "pdef", "pdef_c" )]
partial class PropDropObject : BaseDropObject
{
	PropDefinitionResource propDef;

	protected override Task Initialize( string dragData, CancellationToken token )
	{
		propDef = InstallAsset( dragData, token ).Result.LoadResource<PropDefinitionResource>();
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
			var prefabScene = SceneUtility.GetPrefabScene( prefab );
			GameObject = prefabScene.Clone();
			GameObject.BreakFromPrefab();

			GameObject.WorldPosition = traceTransform.Position;

			if ( GameObject.Components.TryGet<PropLifeComponent>( out var component ) )
			{
				component.Definition = propDef;
				component.ApplyResourceToProp();
				GameObject.Name = propDef.Name;
				GameObject.MakeNameUnique();
			}

			EditorScene.Selection.Clear();
			EditorScene.Selection.Add( GameObject );
		}

		return Task.CompletedTask;
	}
}
