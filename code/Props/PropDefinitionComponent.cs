using Sandbox;
namespace Physbox;

[Group( "Physbox" )]
[Title( "Prop Definition" )]
[Description("This component holds the Resource for the prop. This exists so we can abstract things away in an editor library.")]
[Icon( "favorite" )]
[Tint( EditorTint.Yellow )]
public class PropDefinitionComponent : Component
{
	[Property] public GameResource Definition { get; set; }
}
