using Sandbox;

[Group( "Physbox" )]
[Title( "Spawnpoint (Physbox)" )]
[Icon( "accessibility" )]
[Tint( EditorTint.Yellow )]
public class PhysboxSpawnpoint : Component
{
	public Color Color { get; set; } = "#E3510D";

	[Property]
	[Description( "This spawn point is valid for any game mode." )]
	public bool AnyGameMode { get; set; } = false;

	[Property]
	[Description( "List of game modes this spawn can use." )]
	[HideIf( "AnyGameMode", true )]
	public List<GameModes> GameModes { get; set; } = new();

	[Property]
	[Description( "If any team can use this spawnpoint" )]
	public bool AnyTeam { get; set; } = true;

	[Property]
	[Description( "The team that can use this spawnpoint." )]
	[HideIf( "AnyTeam", true )]
	public Team Team { get; set; } = Team.Red;

	public bool IsValidSpawnPoint( PlayerComponent player )
	{
		// Invalid spawnpoint if gamemode doesn't match.
		if ( !AnyGameMode && GameModes.Count != 0 && !GameModes.Contains( GameLogicComponent.GameMode ) )
		{
			return false;
		}

		// Invalid spawnpoint if our team does not match.
		if ( GameLogicComponent.UseTeams && !AnyTeam && player.Team != Team )
		{
			return false;
		}

		// Do a quick check to see if there are any players within our range.
		var traces = Scene.Trace.Sphere( 64, new Ray( GameObject.WorldPosition, Vector3.Up ), 72 )
			.WithTag( PhysboxConstants.PlayerTag )
			.WithoutTags( PhysboxConstants.HitboxTag, "clothing" )
			.RunAll();

		foreach ( var trace in traces )
		{
			var go = trace.GameObject;
			if ( go is not null )
			{
				if ( go.Components.TryGet<PlayerComponent>( out var otherPlayer ) )
				{
					// Let's spawn next to our players, that's alright!
					if ( GameLogicComponent.UseTeams && otherPlayer.Team == Team )
					{
						return true;
					}
				}
			}
			else
			{
				return false;
			}
		}

		return true;
	}

	[Button( "Update GameObject Name" )]
	public void UpdateGameObjectName()
	{
		GameObject.Name = "Spawnpoint";
		var additional = "";

		if ( !AnyTeam )
		{
			additional += $"{Team}";
		}

		if ( !AnyGameMode )
		{
			if ( additional != "" )
			{
				additional += ", ";
			}

			additional += $"{string.Join( "& ", GameModes )}";
		}

		if ( additional != "" )
		{
			GameObject.Name += $" ({additional})";
		}
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();
		var model = Model.Load( "models/editor/spawnpoint.vmdl" );
		Gizmo.Hitbox.Model( model );

		var color = AnyTeam ? Color : PhysboxUtilities.GetTeamColor( Team );

		Gizmo.Draw.Color = color.WithAlpha( Gizmo.IsHovered || Gizmo.IsSelected ? 0.7f : 0.5f );
		SceneObject sceneObject = Gizmo.Draw.Model( model, LocalTransform.WithPosition( Vector3.Zero ) );
		if ( sceneObject != null )
		{
			sceneObject.Flags.CastShadows = true;
		}
	}
}
