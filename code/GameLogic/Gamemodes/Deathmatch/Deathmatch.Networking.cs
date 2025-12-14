using Sandbox;

public partial class DeathmatchGameMode
{
	/// <summary>
	/// 
	/// </summary>
	/// <param name="player"></param>
	void IPhysboxNetworkEvents.OnPlayerInitialised( PlayerComponent player )
	{
		// Spawn player into the game if we can allow it.
		player.FreeCam = true;

		if ( !RoundOver )
		{
			player.RequestSpawn();
		}
	}
}
