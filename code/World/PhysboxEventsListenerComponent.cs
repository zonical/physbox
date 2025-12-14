using System;

namespace Sandbox;

[Group( "Physbox" )]
[Title( "Game Events Listener" )]
[Icon( "info" )]
[Tint( EditorTint.Yellow )]
public class PhysboxEventsListenerComponent : Component, IPhysboxGameEvents
{
	[Property] [Title( "On Round Start" )] public OnRoundStartDelegate OnRoundStartAction { get; set; }
	[Property] [Title( "On Round End" )] public OnRoundEndDelegate OnRoundEndAction { get; set; }

	[Property]
	[Title( "On Player Spawn" )]
	public OnPlayerSpawnDelegate OnPlayerSpawnAction { get; set; }

	[Property]
	[Title( "On Player Death" )]
	public OnPlayerDeathDelegate OnPlayerDeathAction { get; set; }

	[Property]
	[Title( "On Player Score Update" )]
	public OnPlayerScoreUpdateDelegate OnPlayerScoreUpdateAction { get; set; }

	void IPhysboxGameEvents.OnRoundStart()
	{
		OnRoundStartAction?.Invoke();
	}

	void IPhysboxGameEvents.OnRoundEnd()
	{
		OnRoundEndAction?.Invoke();
	}

	void IPhysboxGameEvents.OnPlayerSpawn( PlayerComponent player )
	{
		OnPlayerSpawnAction?.Invoke( player );
	}

	void IPhysboxGameEvents.OnPlayerDeath( PhysboxDamageInfo info )
	{
		OnPlayerDeathAction?.Invoke( info );
	}

	void IPhysboxGameEvents.OnPlayerScoreUpdate( PlayerComponent player, int score )
	{
		OnPlayerScoreUpdateAction?.Invoke( player, score );
	}
}
