using Sandbox;

[Group( "Physbox" )]
[Title( "Prop Spawner" )]
[Icon( "add_box" )]
[Tint( EditorTint.Yellow )]
public sealed class PropSpawnerComponent : Component
{
	[Property] public PropDefinitionResource Prop { get; set; }

	[Property, Category( "Trace" )] public float TraceRadius { get; set; } = 128;
	[Property, Category( "Trace" )] public float TraceDistance { get; set; } = 256;
	[Property, Category( "Trace" )] public Vector3 TraceDirection = Vector3.Down;
	[Property, Category( "Trace" )] public Vector3 TraceOffset = new();
	[Property, Category( "Trace" )] public bool Sleeping { get; set; } = false;

	public TimeSince TimeSinceWentToSleep { get; set; } = 0;

	public bool HasSpaceToSpawn()
	{
		// Do a quick check to see if there are any players within our range.
		var traces = Scene.Trace.Cylinder( 1, TraceRadius, new Ray( GameObject.WorldPosition + TraceOffset, TraceDirection ), TraceDistance )
			.WithTag( PhysboxConstants.BreakablePropTag )
			.RunAll();

		foreach ( var trace in traces )
		{
			if ( trace.GameObject is not null && trace.GameObject.Components.TryGet<PropLifeComponent>( out var prop ) )
			{
				if ( prop.Definition.Name == Prop.Name )
				{
					return false;
				}
			}
		}

		return true;
	}

	protected override void DrawGizmos()
	{
		Gizmo.Hitbox.BBox( Prop?.Model?.Bounds ?? Model.Cube.Bounds );

		Gizmo.Draw.Color = Color.White.WithAlpha( 0.5f );
		Gizmo.Draw.Model( Prop?.Model?.Name ?? Model.Cube.Name );

		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.LineCylinder( Vector3.Zero + TraceOffset, Vector3.Zero + TraceOffset + (TraceDirection * TraceDistance), TraceRadius, TraceRadius, 16 );
		}
	}
}
