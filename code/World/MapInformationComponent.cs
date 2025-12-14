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
	[Property] public List<GameModes> SupportedGamemodes { get; set; } = new();

	private string GetGameModeList()
	{
		var list = string.Join( ", ", SupportedGamemodes.Select( gamemode => gamemode.ToString() ) );
		return SupportedGamemodes.Any() ? list : "all";
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
			{ "GameModes", GetGameModeList() },
			{ "PackageIdent", PackageIdent }
		};
	}
}
