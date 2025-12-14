using Sandbox;

public partial class GameLogicComponent
{
	void IPhysboxGameEvents.OnRoundStart()
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		if ( UseTimer )
		{
			TimeShouldEnd = (int)Time.Now + TimerLengthInSeconds;
		}
	}

	void IPhysboxGameEvents.OnRoundEnd()
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		TimeShouldEnd = -1;
		Invoke( RoundIntermissionSeconds, StartGame );
	}
}
