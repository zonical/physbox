using Sandbox;

[Group( "Physbox" )]
[Title( "Spawnpoint (Physbox)" )]
[Icon( "accessibility" )]
[Tint( EditorTint.Yellow )]
public class PhysboxSpawnpoint : Component
{
	public Color Color { get; set; } = "#E3510D";

	[Property, Description( "This spawn point is valid for any game mode." )]
	public bool AnyGameMode { get; set; } = false;

	[Property, Description( "List of game modes this spawn can use." ), HideIf( "AnyGameMode", true )]
	public List<PhysboxConstants.GameModes> GameModes { get; set; } = new();

	public bool IsValidSpawnPoint( PlayerComponent player )
	{
		// Invalid spawnpoint if gamemode doesn't match.
		if ( !AnyGameMode && GameModes.Any() && !GameModes.Contains( GameLogicComponent.GameMode ) )
		{
			return false;
		}

		// TODO: Add team check.

		// Do a quick check to see if there are any players within our range.
		var trace = Scene.Trace.Sphere( 64, new Ray( GameObject.WorldPosition, Vector3.Up ), 72 )
			.WithTag( PhysboxConstants.PlayerTag )
			.Run();

		return trace.GameObject is null;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();
		Model model = Model.Load( "models/editor/spawnpoint.vmdl" );
		Gizmo.Hitbox.Model( model );
		Gizmo.Draw.Color = Color.WithAlpha( (Gizmo.IsHovered || Gizmo.IsSelected) ? 0.7f : 0.5f );
		SceneObject sceneObject = Gizmo.Draw.Model( model, LocalTransform.WithPosition( Vector3.Zero ) );
		if ( sceneObject != null )
		{
			sceneObject.Flags.CastShadows = true;
		}
	}
}
