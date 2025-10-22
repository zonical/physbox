using Sandbox;
using System;

[Hide]
public class BaseGameMode : Component
{
	[Sync, Property, ReadOnly] public int RoundsPlayed { get; set; } = 0;
	[Sync, Property, ReadOnly] public bool RoundOver { get; set; } = false;

	protected GameLogicComponent Game => GameLogicComponent.GetGameInstance();

	public virtual void OnRoundStart()
	{
		RoundsPlayed++;
		RoundOver = false;
	}

	public virtual void OnRoundEnd()
	{
		RoundOver = true;
	}
}
