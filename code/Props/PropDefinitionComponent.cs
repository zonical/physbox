using Sandbox;
namespace Physbox;

public interface IPropDefinitionSubscriber 
{
	public void OnDefinitionChanged( GameResource oldValue, GameResource newValue ) {}
}

[Group( "Physbox" )]
[Title( "Prop Definition" )]
[Description("This component holds the Resource for the prop. This exists so we can abstract things away in an editor library.")]
[Icon( "favorite" )]
[Tint( EditorTint.Yellow )]
public class PropDefinitionComponent : Component
{
	[Property, Change( "OnDefinitionChanged" )] public GameResource Definition { get; set; }

	public void OnDefinitionChanged( GameResource oldValue, GameResource newValue )
	{
		foreach ( var comp in Components.GetAll<IPropDefinitionSubscriber>( 
			FindMode.EverythingInSelf | 
			FindMode.EverythingInAncestors | 
			FindMode.EverythingInChildren ) )
		{
			comp.OnDefinitionChanged( oldValue, newValue );
		}
	}
}
