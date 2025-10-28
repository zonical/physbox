using Editor;
using Sandbox;
using Sandbox.ModelEditor.Nodes;
using System.Collections.Generic;
using System.Text.Json.Serialization;

// Easier to use than Transform, doesn't have any junk in it.
public class ModelTransformOffset
{
	[JsonInclude] public Vector3 Position = Vector3.Zero;
	[JsonInclude] public Rotation Rotation;
	[JsonInclude] public Vector3 Scale = Vector3.One;
}

[EditorForAssetType( "pdef" )]
public class PropDefinitionResourceEditor : DockWindow, IAssetEditor
{
	// Return false if you want the have a Widget created for each Asset opened,
	// Return true if you want only one Widget to be made, calling AssetOpen on the open Widget
	public bool CanOpenMultipleAssets => false;

	// ====================== [ ICON PREVIEW ] ======================
	Scene ScenePreview;
	ModelRenderer PropPreview => ScenePreview.Get<ModelRenderer>();
	ModelTransformOffset PreviewTransformOffset = new();

	// ====================== [ RESOURCE ] ======================
	Asset MyAsset;
	GameResource Resource;
	Dictionary<string, ModelTransformOffset> Offsets;
	Model PreviewModel => Resource.GetSerialized().GetProperty( "Model" ).GetValue<Model>( Model.Error );
	string KillfeedIcon => Resource.GetSerialized().GetProperty( "KillfeedIcon" ).GetValue<string>();

	// ====================== [ WIDGETS ] ======================
	ScrollArea ResourceEditor;
	SceneRenderingWidget SceneRenderer;
	ScrollArea Properties;
	WarningBox NoGibsWarning;
	WarningBox NoIconWarning;
	InformationBox NoWarningBox;

	public void AssetOpen( Asset asset )
	{
		// Get the Resource from the Asset, from here you can get whatever info you want
		MyAsset = asset;
		Resource = MyAsset.LoadResource<GameResource>();

		// Get our saved offsets.
		Offsets = new();

		// Load our offsets file from disk (if we have it).
		var filePath = GetPathToGeneratedIcons() + "saved_icon_offsets.json";
		if ( System.IO.File.Exists( filePath ) )
		{
			var data = System.IO.File.ReadAllText( filePath );
			Offsets = (Dictionary<string, ModelTransformOffset>) Json.Deserialize( data, Offsets.GetType() );
			
			if ( Offsets.ContainsKey( Resource.ResourceName ) )
			{
				Log.Info( $"Found existing PreviewTransformOffset for {Resource.ResourceName}" );
				PreviewTransformOffset = Offsets[ Resource.ResourceName ];
			}
		}

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
		Size = new Vector2( 1024, 640 );

		Show();
		Focus();
	}

	public string GetPathToGeneratedIcons()
	{
		var assetIndex = MyAsset.AbsolutePath.IndexOf( "/assets/" );
		var cutPath = MyAsset.AbsolutePath.Substring( 0, assetIndex + 8 );
		return cutPath + $"materials/ui/props_generated/";
	}

	public void BuildUI()
	{
		// ====================== [ RESOURCE PROPERTIES ] ======================

		ResourceEditor = new ScrollArea( this );
		ResourceEditor.WindowTitle = $"Resource - {Resource.ResourcePath}";
		ResourceEditor.WindowFlags = WindowFlags.Widget;
		
		var propSheet = new ControlSheet();
		var seralizedResource = Resource.GetSerialized();
		propSheet.AddObject( seralizedResource, PropertyFilter );

		ResourceEditor.Canvas = new Widget();
		ResourceEditor.Canvas.Layout = Layout.Column();
		ResourceEditor.Canvas.VerticalSizeMode = SizeMode.Flexible;
		ResourceEditor.Canvas.HorizontalSizeMode = SizeMode.Flexible;
		ResourceEditor.Canvas.Layout.Add( propSheet );
		ResourceEditor.Canvas.Layout.AddStretchCell();
		DockManager.AddDock( null, ResourceEditor, DockArea.Left );

		// ====================== [ RESOURCE WARNINGS ] ======================

		ResourceEditor.Canvas.Layout.AddSeparator();
		ResourceEditor.Canvas.Layout.AddSpacingCell( 8 );

		NoGibsWarning = new WarningBox( ResourceEditor );
		NoGibsWarning.Label.Text = "No gibs are set for this model.";
		ResourceEditor.Canvas.Layout.Add( NoGibsWarning );

		NoIconWarning = new WarningBox( ResourceEditor );
		NoIconWarning.Label.Text = "No killfeed icon is set for this prop. The killfeed will show a blank image when a player is killed with this prop.";
		ResourceEditor.Canvas.Layout.Add( NoIconWarning );

		NoWarningBox = new InformationBox( ResourceEditor );
		NoWarningBox.Label.Text = "No warnings - prop is ready to go!";
		NoWarningBox.BackgroundColor = Theme.Green;
		NoWarningBox.Icon = "check";
		NoWarningBox.Height = 128;
		ResourceEditor.Canvas.Layout.Add( NoWarningBox );

		// ====================== [ ICON PREVIEW ] ======================

		SceneRenderer = new SceneRenderingWidget( this );
		SceneRenderer.WindowTitle = "Killfeed Icon Preview";
		SceneRenderer.MaximumSize = new Vector2( 256, 256 );
		SceneRenderer.WindowFlags = WindowFlags.Widget;
		CreateScene();
		SceneRenderer.Scene = ScenePreview;
		DockManager.AddDock( ResourceEditor, SceneRenderer, DockArea.Right );

		// ====================== [ ICON PROPERTIES ] ======================

		Properties = new ScrollArea( this );
		var transformSheet = new ControlSheet();
		var seralizedTransform = PreviewTransformOffset.GetSerialized();
		transformSheet.AddObject( seralizedTransform, PropertyFilter );

		Properties.Canvas = new Widget();
		Properties.Canvas.Layout = Layout.Column();
		Properties.Canvas.VerticalSizeMode = SizeMode.CanGrow;
		Properties.Canvas.HorizontalSizeMode = SizeMode.Flexible;
		Properties.Canvas.Layout.Add( transformSheet );
		Properties.Canvas.Layout.AddStretchCell();

		Properties.WindowTitle = "Edit Transform";
		Properties.WindowFlags = WindowFlags.Widget;
		DockManager.AddDock( SceneRenderer, Properties, DockArea.Bottom );

		// ====================== [ MENU ] ======================

		var file = MenuBar.AddMenu( "File" );
		var saveOption = file.AddOption( "Save", "common/save.png", Save, "editor.save" );
		saveOption.StatusTip = "Save";
		saveOption.Enabled = true;
		file.AddSeparator();
		file.AddOption( "Open Asset Location", "folder", () => EditorUtility.OpenFileFolder( MyAsset.AbsolutePath ) ).StatusTip = "Open Asset Location";
		file.AddSeparator();
		file.AddOption( "Quit", null, Quit, "editor.quit" ).StatusTip = "Quit";

		var icon = MenuBar.AddMenu( "Killfeed Icon" );
		var generateKillfeedIcon = icon.AddOption( "Generate Killfeed Icon", "common/save.png", GenerateKillfeedIcon );
		generateKillfeedIcon.StatusTip = "Generate Killfeed Icon";
		generateKillfeedIcon.Enabled = true;
		icon.AddSeparator();
		icon.AddOption( "Open Generated Icons Folder", "folder", OpenGeneratedFoldersIcon ).StatusTip = " Generated Icons Folder";
	}

	[EditorEvent.Frame]
	public void Tick()
	{
		// Update icon preview.
		using ( ScenePreview.Push() )
		{
			PropPreview.Model = PreviewModel;
			PropPreview.WorldPosition = PreviewTransformOffset.Position;
			PropPreview.WorldRotation = PreviewTransformOffset.Rotation;
			PropPreview.WorldScale = PreviewTransformOffset.Scale;

			ScenePreview.EditorTick( Time.Now, Time.Delta );
		}

		// Show warning if no gibs are set for this model.
		var breaklist = PreviewModel?.GetData<ModelBreakPiece[]>();
		NoGibsWarning.Visible = breaklist is null || breaklist.Length == 0;

		// Show warning if no icon is set.
		NoIconWarning.Visible = KillfeedIcon is null || string.IsNullOrEmpty( KillfeedIcon );

		// Show information box if there are no warnings.
		NoWarningBox.Visible = !NoGibsWarning.Visible && !NoIconWarning.Visible;
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
		}
	}

	[Shortcut( "editor.save", "CTRL+S" )]
	public void Save()
	{
		// Save resource.
		var json = Resource.Serialize().ToJsonString();
		if ( string.IsNullOrWhiteSpace( json ) )
			return;

		System.IO.File.WriteAllText( MyAsset.AbsolutePath, json );

		// Save offsets.
		var offsetsJson = Json.Serialize( Offsets );
		if ( string.IsNullOrWhiteSpace( offsetsJson ) )
			return;

		System.IO.File.WriteAllText( GetPathToGeneratedIcons() + "saved_icon_offsets.json", offsetsJson );
	}
	public void GenerateKillfeedIcon()
	{
		using ( ScenePreview.Push() )
		{
			var bitmap = new Bitmap( 256, 256 );
			ScenePreview.Camera.RenderToBitmap( bitmap );
			var data = bitmap.ToPng();

			Log.Info( MyAsset.AbsolutePath );

			// Create a new path based on AbsolutePath.
			var filePath = GetPathToGeneratedIcons();

			// Ensure the path exists.
			System.IO.Directory.CreateDirectory( filePath );

			// Write the file.
			var stream = System.IO.File.OpenWrite( filePath + $"{Resource.ResourceName}.png" );
			stream.Write( data );
			stream.Close();

			var iconPath = $"materials/ui/props_generated/{Resource.ResourceName}.png";
			Resource.GetSerialized().GetProperty( "KillfeedIcon" ).SetValue<string>( iconPath );
		}

		// Save the settings we just used.
		Offsets[Resource.ResourceName] = PreviewTransformOffset;

		Save();
	}

	public void OpenGeneratedFoldersIcon()
	{
		// Create a new path based on AbsolutePath.
		var filePath = GetPathToGeneratedIcons();

		// Ensure the path exists.
		System.IO.Directory.CreateDirectory( filePath );

		EditorUtility.OpenFileFolder( filePath + $"{Resource.ResourceName}.png" );
	}

	// Stolen from shadergraph.
	bool PropertyFilter( SerializedProperty property )
	{
		if ( property.HasAttribute<JsonIgnoreAttribute>() ) return false;
		return true;
	}
}
