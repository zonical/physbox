using Editor;
using Editor.TextureEditor;
using Sandbox;
using System.Text.Json.Serialization;

public class ModelTransformOffset
{
	public Vector3 Position = Vector3.Zero;
	public Rotation Rotation;
	public Vector3 Scale = Vector3.One;

	public override string ToString()
	{
		return $"Pos: {Position}, Rotation: {Rotation}, Scale: {Scale}";
	}
}

[EditorForAssetType( "pdef" )]
public class PropDefinitionResourceEditor : DockWindow, IAssetEditor
{
	Scene ScenePreview;
	ModelRenderer PropPreview => ScenePreview.Get<ModelRenderer>();
	ModelTransformOffset PreviewTransformOffset = new();

	// Return false if you want the have a Widget created for each Asset opened,
	// Return true if you want only one Widget to be made, calling AssetOpen on the open Widget
	public bool CanOpenMultipleAssets => false;

	Asset MyAsset;
	PropDefinitionResource Resource;

	ScrollArea ResourceEditor;

	public void AssetOpen( Asset asset )
	{
		// Get the Resource from the Asset, from here you can get whatever info you want
		MyAsset = asset;
		Resource = MyAsset.LoadResource<PropDefinitionResource>();

		BuildUI();

		WindowTitle = $"Prop Definition Editor - {Resource.ResourcePath}";
	}

	// From IAssetEditor
	public void SelectMember( string memberName ) { }

	public PropDefinitionResourceEditor()
	{
		WindowTitle = "Hello";
		WindowFlags = WindowFlags.Dialog | WindowFlags.Customized | WindowFlags.CloseButton | WindowFlags.WindowSystemMenuHint | WindowFlags.WindowTitle | WindowFlags.MaximizeButton;
		SetWindowIcon( "inventory_2" );
		Size = new Vector2( 720, 680 );

		Show();
		Focus();
	}

	public void BuildUI()
	{
		// A list of all the files.
		ResourceEditor = new ScrollArea( this );
		ResourceEditor.WindowTitle = $"Resource - {Resource.ResourcePath}";
		var propSheet = new ControlSheet();
		var seralizedResource = Resource.GetSerialized();
		propSheet.AddObject( seralizedResource, PropertyFilter );

		ResourceEditor.Canvas = new Widget();
		ResourceEditor.Canvas.Layout = Layout.Column();
		ResourceEditor.Canvas.VerticalSizeMode = SizeMode.CanGrow;
		ResourceEditor.Canvas.HorizontalSizeMode = SizeMode.Flexible;
		ResourceEditor.Canvas.Layout.Add( propSheet );
		ResourceEditor.Canvas.Layout.AddStretchCell();
		DockManager.AddDock( null, ResourceEditor, DockArea.Left );

		// The rendering preview for the prop.
		var sceneView = new SceneRenderingWidget( this );
		sceneView.WindowTitle = "Killfeed Icon Preview";
		sceneView.MaximumSize = new Vector2( 256, 256 );
		CreateScene();
		sceneView.Scene = ScenePreview;
		DockManager.AddDock( ResourceEditor, sceneView, DockArea.Right );

		// Properties, manipulating stuff.
		var properties = new ScrollArea( this );
		var transformSheet = new ControlSheet();

		PreviewTransformOffset.Position = Resource.KillfeedIconPosition;
		PreviewTransformOffset.Rotation = Resource.KillfeedIconRotation;
		PreviewTransformOffset.Scale = Resource.KillfeedIconScale;

		var seralizedTransform = PreviewTransformOffset.GetSerialized();
		transformSheet.AddObject( seralizedTransform, PropertyFilter );

		properties.Canvas = new Widget();
		properties.Canvas.Layout = Layout.Column();
		properties.Canvas.VerticalSizeMode = SizeMode.CanGrow;
		properties.Canvas.HorizontalSizeMode = SizeMode.Flexible;
		properties.Canvas.Layout.Add( transformSheet );
		properties.Canvas.Layout.AddStretchCell();

		properties.WindowTitle = "Edit Transform";
		DockManager.AddDock( sceneView, properties, DockArea.Bottom );

		var buttonDock = new ScrollArea( this );
		buttonDock.Canvas = new Widget();
		buttonDock.Canvas.Layout = Layout.Column();

		var button = new Button();
		button.Pressed += () =>
		{
			using ( ScenePreview.Push() )
			{
				var bitmap = new Bitmap( 256, 256 );
				ScenePreview.Camera.RenderToBitmap( bitmap );
				var data = bitmap.ToPng();

				Log.Info( MyAsset.AbsolutePath );

				// Create a new path based on AbsolutePath.
				var assetIndex = MyAsset.AbsolutePath.IndexOf( "/assets/" );
				var cutPath = MyAsset.AbsolutePath.Substring( 0, assetIndex + 8 );
				var filePath = cutPath + $"materials/ui/props_generated/";

				// Ensure the path exists.
				System.IO.Directory.CreateDirectory( filePath );

				// Write the file.
				var stream = System.IO.File.OpenWrite( filePath + $"{PropPreview.Model.ResourceName}.png" );
				stream.Write( data );
				stream.Close();

				Resource.KillfeedIcon = $"materials/ui/props_generated/{PropPreview.Model.ResourceName}.png";
			}

			// Save the settings we just used.
			Resource.KillfeedIconPosition = PreviewTransformOffset.Position;
			Resource.KillfeedIconRotation = PreviewTransformOffset.Rotation;
			Resource.KillfeedIconScale = PreviewTransformOffset.Scale;

			Save();
		};
		button.Text = "Generate Killfeed Icon";

		buttonDock.Canvas.Layout.Add( button );
		buttonDock.Canvas.Layout.AddStretchCell();
		buttonDock.WindowTitle = "Actions";
		DockManager.AddDock( properties, buttonDock, DockArea.Bottom );

		// Menu bar.
		var file = MenuBar.AddMenu( "File" );
		var saveOption = file.AddOption( "Save", "common/save.png", Save, "editor.save" );
		saveOption.StatusTip = "Save";
		saveOption.Enabled = true;
		file.AddSeparator();
		file.AddOption( "Open Asset Location", "folder", () => EditorUtility.OpenFileFolder( MyAsset.AbsolutePath ) ).StatusTip = "Open Asset Location";
		file.AddSeparator();
		file.AddOption( "Quit", null, Quit, "editor.quit" ).StatusTip = "Quit";
	}

	[EditorEvent.Frame]
	public void UpdatePosition()
	{
		using ( ScenePreview.Push() )
		{
			PropPreview.WorldPosition = PreviewTransformOffset.Position;
			PropPreview.WorldRotation = PreviewTransformOffset.Rotation;
			PropPreview.WorldScale = PreviewTransformOffset.Scale;

			ScenePreview.EditorTick( Time.Now, Time.Delta );
		}
	}

	[Shortcut( "editor.quit", "CTRL+Q" )]
	void Quit()
	{
		Close();
	}

	public void CreateScene()
	{
		// Create a scene.
		ScenePreview = Scene.CreateEditorScene();

		using ( ScenePreview.Push() )
		{
			ScenePreview.LoadFromFile( "scenes/preview.scene" );
			var camera = ScenePreview.Camera;
			camera.BackgroundColor = Color.Transparent;

			PropPreview.Model = Resource.Model;

			PreviewTransformOffset.Position = ScenePreview.WorldPosition;
			PreviewTransformOffset.Rotation = ScenePreview.WorldRotation;
			PreviewTransformOffset.Scale = ScenePreview.WorldScale;
		}
	}

	[Shortcut( "editor.save", "CTRL+S" )]
	public void Save()
	{
		var json = Resource.Serialize().ToJsonString();
		if ( string.IsNullOrWhiteSpace( json ) )
			return;

		System.IO.File.WriteAllText( MyAsset.AbsolutePath, json );
	}

	// Stolen from shadergraph.
	bool PropertyFilter( SerializedProperty property )
	{
		if ( property.HasAttribute<JsonIgnoreAttribute>() ) return false;
		return true;
	}
}
