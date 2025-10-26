using Sandbox;
using System.Text.Json.Serialization;

[GameResource( "Prop Definition", "pdef", "Defines a prop for Physbox", Icon = "inventory_2" )]
public class PropDefinitionResource : GameResource
{
	public string Name { get; set; }
	public Model Model { get; set; }
	public int Mass { get; set; } = 50;
	public int MaxHealth { get; set; } = 100;
	public Vector3 HeldPositionOffset { get; set; } = Vector3.Zero;
	public Angles HeldRotationOffset { get; set; } = Angles.Zero;
	[ImageAssetPath] public string KillfeedIcon { get; set; }
}
