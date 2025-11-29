using System.Text.Json.Nodes;
using Sandbox;

namespace Physbox;

public interface IPropDefinitionSubscriber
{
	public void OnDefinitionChanged( PropDefinitionResource oldValue, PropDefinitionResource newValue ) { }
}

[Group( "Physbox" )]
[Title( "Prop Definition" )]
[Description(
	"This component holds the Resource for the prop. This exists so we can abstract things away in an editor library." )]
[Icon( "favorite" )]
[Tint( EditorTint.Yellow )]
public class PropDefinitionComponent : Component
{
	[Property]
	[Change( "OnDefinitionChanged" )]
	public PropDefinitionResource Definition { get; set; }

	public void OnDefinitionChanged( PropDefinitionResource oldValue, PropDefinitionResource newValue )
	{
		foreach ( var comp in Components.GetAll<IPropDefinitionSubscriber>(
			         FindMode.EverythingInSelf |
			         FindMode.EverythingInAncestors |
			         FindMode.EverythingInChildren ) )
		{
			comp.OnDefinitionChanged( oldValue, newValue );
		}
	}

	public override int ComponentVersion => 1;

	/// <summary>
	/// Defines the first upgrade for MyComponent, which deletes StringProperty and replaces it with NewStringProperty.
	/// </summary>
	/// <param name="json"></param>
	[JsonUpgrader( typeof(PropDefinitionComponent), 1 )]
	private static void StringPropertyUpgrader( JsonObject json )
	{
		json.Remove( "Definition", out var newNode );
		json["Definition"] = newNode;
	}
}
