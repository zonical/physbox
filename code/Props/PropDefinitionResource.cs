using System;
using Sandbox;
using System.Text.Json.Serialization;
namespace Physbox;

public delegate void OnPropBrokenDelegate( GameObject prop );

[AssetType( Name = "Prop Definition", Extension = "pdef", Category = "Physbox" )]
public class PropDefinitionResource : GameResource
{
	public string Name { get; set; }
	public Model Model { get; set; }
	public int Mass { get; set; } = 50;
	public int MaxHealth { get; set; } = 100;
	public Vector3 HeldPositionOffset { get; set; } = Vector3.Zero;
	public Angles HeldRotationOffset { get; set; } = Angles.Zero;
	[ImageAssetPath] public string KillfeedIcon { get; set; }

	[SingleAction] public OnPropBrokenDelegate OnPropBroken { get; set; }

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "inventory_2", width, height, "#fdea60", "black" );
	}
}
