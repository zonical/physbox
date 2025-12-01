using Sandbox;

[Group( "Physbox" )]
[Title( "Killfeed Manager" )]
[Icon( "directions_run" )]
[Tint( EditorTint.Yellow )]
[Hide]
public class KillfeedManagerComponent : Component, IGameEvents
{
	[Property] public List<(PlayerComponent, PhysboxDamageInfo, TimeSince)> Deaths { get; set; } = new();

	protected override void OnEnabled()
	{
		// Delete ourselves if we're in the main menu.
		if ( PhysboxUtilites.IsMainMenuScene() )
		{
			DestroyGameObject();
			return;
		}
	}

	[Rpc.Broadcast] // Make everyone update their killfeeds.
	void IGameEvents.OnRoundStart()
	{
		Deaths.Clear();
	}

	[Rpc.Broadcast] // Make everyone update their killfeeds.
	void IGameEvents.OnPlayerDeath( PhysboxDamageInfo info )
	{
		Log.Info( $"{info.Victim?.Name} died to {info.Attacker?.Name}, caused by {info.Prop}" );

		var timeSince = new TimeSince();
		timeSince = 0;

		Deaths.Add( (info.Victim, info, timeSince) );
	}

	protected override void OnUpdate()
	{
		// Remove the death from the killfeed after five seconds.
		for ( var i = 0; i < Deaths.Count; i++ )
		{
			var death = Deaths[i];
			if ( death.Item3.Relative >= 5 )
			{
				RemoveDeathFromList( i );

				// Don't continue after we remove a death, as this can cause
				// a collection related InvalidOperationException.
				break;
			}
		}
	}

	[Rpc.Broadcast] // Make everyone update their killfeeds.
	private void RemoveDeathFromList( int index )
	{
		if ( !Deaths.Any() || index > Deaths.Count )
		{
			return;
		}

		Deaths.RemoveAt( index );
	}
}
