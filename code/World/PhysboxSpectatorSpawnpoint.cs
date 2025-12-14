using Sandbox;

[Group( "Physbox" )]
[Title( "Spectator Spawnpoint (Physbox)" )]
[Icon( "accessibility" )]
[Tint( EditorTint.Yellow )]
public class PhysboxSpectatorSpawnpoint : Component
{
	protected override void DrawGizmos()
	{
		var model = Model.Load( "models/editor/camera.vmdl" );
		Gizmo.Hitbox.Model( model );

		Gizmo.Draw.Color = Color.White.WithAlpha( Gizmo.IsHovered || Gizmo.IsSelected ? 0.7f : 0.5f );

		var sceneObject = Gizmo.Draw.Model( model );
		if ( sceneObject != null )
		{
			sceneObject.Flags.CastShadows = true;
		}
	}
}
