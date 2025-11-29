[Group( "Physbox" )]
[Title( "Map Information" )]
[Icon( "info" )]
[Tint( EditorTint.Yellow )]
public class MapInformationComponent : Component, ISceneMetadata
{
	[Property]
	[ImageAssetPath]
	[ShowIf( "PackageIdent", "" )]
	public string Image { get; set; }

	[Property] public string PackageIdent { get; set; } = "";
	[Property] public bool IsVisibleInMenu { get; set; } = true;
	[Property] public bool OverrideDefaultSpawnpoints { get; set; } = false;

	private List<string> GetGameModeList()
	{
		var list = new List<string>();
		/*foreach ( var go in Scene.GetAllObjects( false ) )
		{
			var gm = go.GetComponent<GameMode>( true );
			if ( !gm.IsValid() ) continue;
			list.Add( go.PrefabInstanceSource );
		}*/

		return list;
	}

	protected override void OnValidate()
	{
		foreach ( var spawnpoint in Scene.GetComponents<SpawnPoint>( true ) )
		{
			spawnpoint.Enabled = !OverrideDefaultSpawnpoints;
		}
	}

	Dictionary<string, string> ISceneMetadata.GetMetadata()
	{
		return new Dictionary<string, string>
		{
			{ "Image", Image },
			{ "IsVisibleInMenu", IsVisibleInMenu.ToString() },
			{ "GameModes", string.Join( ", ", GetGameModeList() ) },
			{ "PackageIdent", PackageIdent }
		};
	}
}
