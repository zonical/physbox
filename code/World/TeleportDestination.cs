using Sandbox;

[Group( "Physbox" )]
[Title( "Teleporter Destination" )]
[Icon( "accessibility" )]
[Tint( EditorTint.Yellow )]
public class TeleportDestination : Component
{
	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Model model = Model.Load( "models/editor/spawnpoint.vmdl" );
		Gizmo.Hitbox.Model( model );
		Gizmo.Draw.Color = Color.Green.WithAlpha( (Gizmo.IsHovered || Gizmo.IsSelected) ? 0.7f : 0.5f );

		SceneObject sceneObject = Gizmo.Draw.Model( model );

		if ( sceneObject != null )
		{
			sceneObject.Flags.CastShadows = true;
		}
	}
}
