using Sandbox;

public partial class GameLogicComponent
{
	[ActionGraphNode( "physbox.get_game_instance" )]
	[Title( "Get Game Instance" ), Group( "Physbox" ), Icon( "home" )]
	public static GameLogicComponent GetGameInstance()
	{
		return Game.ActiveScene.Get<GameLogicComponent>();
	}

	[ActionGraphNode( "physbox.set_timer_state" )]
	[Title( "Set Timer State" ), Group( "Physbox" ), Icon( "alarm" )]
	public static void SetTimerState( bool state )
	{
		UseTimer = state;
	}

	[ActionGraphNode( "physbox.add_time" )]
	[Title( "Add Time to Round" ), Group( "Physbox" ), Icon( "alarm" )]
	public static void AddTime( int time )
	{
		GetGameInstance().TimeShouldEnd += time;
	}

	[ActionGraphNode( "physbox.remove_time" )]
	[Title( "Remove Time to Round" ), Group( "Physbox" ), Icon( "alarm" )]
	public static void RemoveTime( int time )
	{
		GetGameInstance().TimeShouldEnd -= time;
	}
}
