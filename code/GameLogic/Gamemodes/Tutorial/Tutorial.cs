using Sandbox;
using System;

[Hide]
public class TutorialGameMode : BaseGameMode, IPhysboxGameEvents, IPhysboxNetworkEvents
{
	/// <summary>
	/// Spawns the player straight away after initalisation.
	/// </summary>
	/// <param name="player"></param>
	void IPhysboxNetworkEvents.OnPlayerInitialised( PlayerComponent player )
	{
		// Spawn player into the game if we can allow it.
		player.FreeCam = true;
		player.RequestSpawn();
	}

	/// <summary>
	/// Respawn the player immediately. We shouldn't be able to die in the tutorial.
	/// </summary>
	/// <param name="info"></param>
	void IPhysboxGameEvents.OnPlayerDeath( PhysboxDamageInfo info )
	{
		info.Victim?.RequestSpawn();
	}

	/// <summary>
	/// Spawn the player on round start.
	/// </summary>
	void IPhysboxGameEvents.OnRoundStart()
	{
		base.OnRoundStart();

		foreach ( var player in Scene.GetAllComponents<PlayerComponent>().Where( x => !x.IsAlive ) )
		{
			player.Spawn();
		}
	}
}
