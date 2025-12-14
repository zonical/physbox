using Sandbox;

public partial class GameLogicComponent
{
	// The exact time this timer should end.
	// This value is set in each gamemode component.
	[Property]
	[ReadOnly]
	[Sync]
	[ActionGraphIgnore]
	public int TimeShouldEnd { get; set; } = -1;


	[Property]
	[ReadOnly]
	[Title( "Time Remaining" )]
	[Icon( "alarm" )]
	public int TimeLeft => TimeShouldEnd - (int)Time.Now;
}
