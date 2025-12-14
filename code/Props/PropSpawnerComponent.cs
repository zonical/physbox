using Sandbox;
using Physbox;

[Group( "Physbox" )]
[Title( "Prop Spawner" )]
[Icon( "add_box" )]
[Tint( EditorTint.Yellow )]
public sealed class PropSpawnerComponent : Component
{
	[Property] public PropDefinitionResource Prop { get; set; }
	[Property] public bool SpawnImmediately { get; set; }

	[Property]
	[Description( "This spawner is valid for any game mode." )]
	public bool AnyGameMode { get; set; } = true;

	[Property]
	[Description( "List of game modes this spawner can use." )]
	[HideIf( "AnyGameMode", true )]
	public List<GameModes> GameModes { get; set; } = new();

	[Property] [Category( "Trace" )] public float TraceRadius { get; set; } = 128;
	[Property] [Category( "Trace" )] public float TraceDistance { get; set; } = 256;
	[Property] [Category( "Trace" )] public Vector3 TraceDirection = Vector3.Down;
	[Property] [Category( "Trace" )] public Vector3 TraceOffset = new();
	[Property] [Category( "Trace" )] public bool Sleeping { get; set; } = false;

	public TimeSince TimeSinceWentToSleep { get; set; } = 0;

	public bool HasSpaceToSpawn()
	{
		if ( GameObject is null || GameObject.IsDeserializing || GameObject.Flags.Contains( GameObjectFlags.Loading ) )
		{
			return false;
		}

		// Do a quick check to see if there are any players within our range.
		var traces = Scene.Trace.Cylinder( 1, TraceRadius,
				new Ray( GameObject.WorldPosition + TraceOffset, TraceDirection ), TraceDistance )
			.WithTag( PhysboxConstants.BreakablePropTag )
			.RunAll();

		foreach ( var trace in traces )
		{
			if ( trace.GameObject is not null &&
			     trace.GameObject.Components.TryGet<PropDefinitionComponent>( out var prop ) )
			{
				var def = prop.Definition as PropDefinitionResource;
				if ( def.Name == Prop.Name )
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
			Gizmo.Draw.LineCylinder( Vector3.Zero + TraceOffset,
				Vector3.Zero + TraceOffset + TraceDirection * TraceDistance, TraceRadius, TraceRadius, 16 );
		}
	}

	[Button( "Update GameObject Name" )]
	public void UpdateGameObjectName()
	{
		GameObject.Name = $"Spawner ({Prop.Name})";
	}
}
