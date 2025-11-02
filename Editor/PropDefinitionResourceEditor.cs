using Editor;
using Editor.Assets;
using Physbox;
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
[EditorApp( "Prop Definition Editor", "inventory_2", "Edit props for Physbox." )]
public class PropDefinitionResourceEditor : DockWindow, IAssetEditor
{
	// Return false if you want the have a Widget created for each Asset opened,
	// Return true if you want only one Widget to be made, calling AssetOpen on the open Widget
	public bool CanOpenMultipleAssets => false;

	// ====================== [ ICON PREVIEW ] ======================
	Scene KillfeedScenePreview;
	Scene FirstPersonScenePreview;
	ModelRenderer KillfeedPropPreview => KillfeedScenePreview.Get<ModelRenderer>();
	ModelRenderer FirstPersonPropPreview;
	ModelTransformOffset PreviewTransformOffset = new();

	// ====================== [ RESOURCE ] ======================
	Asset MyAsset;
	GameResource Resource;
	Dictionary<string, ModelTransformOffset> Offsets;
	Model PreviewModel => Resource.GetSerialized().GetProperty( "Model" ).GetValue<Model>( Model.Error );
	string KillfeedIcon => Resource.GetSerialized().GetProperty( "KillfeedIcon" ).GetValue<string>();
	Vector3 HeldPositionOffset => Resource.GetSerialized().GetProperty( "HeldPositionOffset" ).GetValue<Vector3>();
	Angles HeldRotationOffset => Resource.GetSerialized().GetProperty( "HeldRotationOffset" ).GetValue<Angles>();

	// ====================== [ WIDGETS ] ======================
	ScrollArea ResourceEditor;
	SceneRenderingWidget KillfeedSceneRenderer;
	SceneRenderingWidget FirstPersonSceneRenderer;
	ScrollArea Properties;
	WarningBox NoGibsWarning;
	WarningBox NoIconWarning;
	InformationBox NoWarningBox;
	ControlSheet ResourceControlSheet;

	bool NeedsSave = false;

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

		WindowTitle = $"Prop Definition Editor - ({Resource.ResourcePath})";
		ResourceEditor.WindowTitle = $"Resource - {Resource.ResourcePath}";

		var seralizedResource = Resource.GetSerialized();
		ResourceControlSheet.Clear(true);
		ResourceControlSheet.AddObject( seralizedResource, PropertyFilter );
	}

	// From IAssetEditor
	public void SelectMember( string memberName ) { }

	public PropDefinitionResourceEditor()
	{
		Resource = CreatePlaceholderResource();

		WindowTitle = $"Prop Definition Editor - (untitled)";
		WindowFlags = WindowFlags.Dialog | WindowFlags.Customized | WindowFlags.CloseButton | WindowFlags.WindowSystemMenuHint | WindowFlags.WindowTitle | WindowFlags.MaximizeButton;
		SetWindowIcon( "inventory_2" );
		Size = new Vector2( 1024, 640 );

		BuildUI();
		Show();
		Focus();
	}

	public PropDefinitionResource CreatePlaceholderResource()
	{
		var placeholder = new PropDefinitionResource();

		placeholder.Name = "My Amazing Prop";
		placeholder.Model = Model.Error;
		placeholder.Mass = 100;
		placeholder.MaxHealth = 100;

		return placeholder;
	}

	public string GetPathToGeneratedIcons()
	{
		var assetIndex = MyAsset.AbsolutePath.IndexOf( "/assets/" );
		var cutPath = MyAsset.AbsolutePath.Substring( 0, assetIndex + 8 );
		return cutPath + $"materials/ui/props_generated/";
	}

	public void BuildUI()
	{
		DockManager.Clear();
		MenuBar.Clear();

		// ====================== [ RESOURCE PROPERTIES ] ======================

		ResourceEditor = new ScrollArea( this );
		ResourceEditor.WindowTitle = $"Resource - {Resource.ResourcePath}";
		ResourceEditor.WindowFlags = WindowFlags.Widget;

		ResourceControlSheet = new ControlSheet();
		var seralizedResource = Resource.GetSerialized();
		ResourceControlSheet.AddObject( seralizedResource, PropertyFilter );

		ResourceEditor.Canvas = new Widget();
		ResourceEditor.Canvas.Layout = Layout.Column();
		ResourceEditor.Canvas.VerticalSizeMode = SizeMode.CanShrink;
		ResourceEditor.Canvas.HorizontalSizeMode = SizeMode.CanShrink;
		ResourceEditor.Canvas.Layout.Add( ResourceControlSheet );
		ResourceEditor.Canvas.Layout.AddStretchCell();
		DockManager.AddDock( null, ResourceEditor, DockArea.Left );

		// ====================== [ FIRST PERSON PREVIEW ] ======================

		/*ResourceEditor.Canvas.Layout.AddSeparator();
		ResourceEditor.Canvas.Layout.AddSpacingCell( 8 );

		FirstPersonSceneRenderer = new SceneRenderingWidget( ResourceEditor );
		FirstPersonSceneRenderer.VerticalSizeMode = SizeMode.Flexible;
		FirstPersonSceneRenderer.HorizontalSizeMode = SizeMode.Flexible;
		CreateFirstPersonPreviewScene();
		FirstPersonSceneRenderer.Scene = FirstPersonScenePreview;
		ResourceEditor.Canvas.Layout.Add( FirstPersonSceneRenderer );*/

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

		KillfeedSceneRenderer = new SceneRenderingWidget( this );
		KillfeedSceneRenderer.WindowTitle = "Killfeed Icon Preview";
		KillfeedSceneRenderer.MaximumSize = new Vector2( 256, 256 );
		KillfeedSceneRenderer.WindowFlags = WindowFlags.Widget;
		CreateKillfeedPreviewScene();
		KillfeedSceneRenderer.Scene = KillfeedScenePreview;
		DockManager.AddDock( ResourceEditor, KillfeedSceneRenderer, DockArea.Right );

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
		DockManager.AddDock( KillfeedSceneRenderer, Properties, DockArea.Bottom );

		// ====================== [ MENU ] ======================

		var file = MenuBar.AddMenu( "File" );
		var openOption = file.AddOption( "Open", "common/open.png", Open );
		openOption.StatusTip = "Open Sprite";

		var saveOption = file.AddOption( "Save", "common/save.png", Save, "editor.save" );
		saveOption.StatusTip = "Save";
		saveOption.Enabled = true;

		var saveAsOption = file.AddOption( "Save As", "common/save.png", Save );
		saveAsOption.StatusTip = "Save As";
		saveAsOption.Enabled = true;

		file.AddSeparator();

		var assetLocationOption = file.AddOption( "Open Asset Location", "folder", () => EditorUtility.OpenFileFolder( MyAsset.AbsolutePath ) );
		assetLocationOption.StatusTip = "Open Asset Location";
		assetLocationOption.Enabled = true;

		file.AddSeparator();

		var restoreUI = file.AddOption( "Restore Dock Layout", "common/refresh.png", BuildUI );
		restoreUI.StatusTip = "Save As";
		restoreUI.Enabled = true;

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
		using ( KillfeedScenePreview.Push() )
		{
			KillfeedPropPreview.Model = PreviewModel;
			KillfeedPropPreview.WorldPosition = PreviewTransformOffset.Position;
			KillfeedPropPreview.WorldRotation = PreviewTransformOffset.Rotation;
			KillfeedPropPreview.WorldScale = PreviewTransformOffset.Scale;

			KillfeedScenePreview.EditorTick( Time.Now, Time.Delta );
		}

		// Update first person preview.
		/*using ( FirstPersonScenePreview.Push() )
		{
			FirstPersonPropPreview.Model = PreviewModel;

			var camera = FirstPersonScenePreview.Camera;
			var targetPos = camera.WorldPosition - new Vector3( 0, 0, 16 ) + (camera.WorldRotation.Forward * 64);

			FirstPersonPropPreview.WorldPosition = targetPos + HeldPositionOffset;
			FirstPersonPropPreview.WorldRotation = HeldRotationOffset;
		}*/

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

	public void CreateKillfeedPreviewScene()
	{
		// Create a scene.
		KillfeedScenePreview = Scene.CreateEditorScene();

		using ( KillfeedScenePreview.Push() )
		{
			KillfeedScenePreview.LoadFromFile( "scenes/killfeed_preview.scene" );
			var camera = KillfeedScenePreview.Camera;
			camera.BackgroundColor = Color.Transparent;
		}
	}

	public void CreateFirstPersonPreviewScene()
	{
		// Create a scene.
		FirstPersonScenePreview = Scene.CreateEditorScene();

		using ( FirstPersonScenePreview.Push() )
		{
			FirstPersonScenePreview.LoadFromFile( "scenes/firstperson_preview.scene" );
			var camera = FirstPersonScenePreview.Camera;
			camera.BackgroundColor = Color.Transparent;
			camera.FieldOfView = 100;

			// Create arms.
			var viewModel = new GameObject( true, "Viewmodel" );

			var modelComp = viewModel.AddComponent<SkinnedModelRenderer>();
			modelComp.Model = Cloud.Model( "facepunch.v_first_person_arms_human" );
			modelComp.RenderOptions.Overlay = true;
			modelComp.RenderOptions.Game = false;
			modelComp.RenderType = ModelRenderer.ShadowRenderType.Off;
			modelComp.UseAnimGraph = true;

			viewModel.WorldRotation = new Angles( 45, 0, 5 );
			viewModel.WorldPosition = camera.WorldPosition - new Vector3( 0, 0, 2 ) + camera.WorldRotation.Forward * 8;

			// Create first person model.
			var firstPersonModel = new GameObject( true, "Object" );
			FirstPersonPropPreview = firstPersonModel.AddComponent<ModelRenderer>();
			FirstPersonPropPreview.Model = PreviewModel;

		}
	}

	[Shortcut( "editor.open", "CTRL+O", ShortcutType.Window )]
	public void Open()
	{
		var fileDialogue = new FileDialog( null )
		{
			Title = "Open Prop Definition",
			DefaultSuffix = ".pdef"
		};
		fileDialogue.SetNameFilter( "Prop Definition (*.pdef)" );

		if ( !fileDialogue.Execute() ) return;

		AssetOpen( AssetSystem.FindByPath( fileDialogue.SelectedFile ) );
	}

	[Shortcut( "editor.save", "CTRL+S" )]
	public void Save()
	{
		if ( MyAsset is null )
		{
			SaveAs();
			return;
		}

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

		WindowTitle = $"Prop Definition Editor - ({Resource.ResourcePath})";
	}

	public void SaveAs()
	{
		var fileDialogue = new FileDialog( null )
		{
			Title = "Open Prop Definition",
			DefaultSuffix = ".pdef",
		
		};
		fileDialogue.SetNameFilter( "Prop Definition (*.pdef)" );
		fileDialogue.SetModeSave();

		if ( !fileDialogue.Execute() ) return;


		MyAsset ??= AssetSystem.CreateResource( "pdef", fileDialogue.SelectedFile );
		Save();

		MainAssetBrowser.Instance?.Local?.UpdateAssetList();
	}

	public void GenerateKillfeedIcon()
	{
		using ( KillfeedScenePreview.Push() )
		{
			var bitmap = new Bitmap( 256, 256 );
			KillfeedScenePreview.Camera.RenderToBitmap( bitmap );
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
