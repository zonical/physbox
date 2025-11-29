using Sandbox;

[Hide]
public class EmptyGameMode : BaseGameMode
{
	public void OnPlayerSpawn( PlayerComponent player )
	{
		Log.Info( "EmptyGameMode.OnPlayerSpawned()" );
	}

	public void OnPlayerDeath( PlayerComponent player, GameObject attacker )
	{
		// Respawn shortly.
		Log.Info( "EmptyGameMode.OnPlayerDied()" );
		player.Invoke( PlayerConvars.RespawnTime, player.Spawn );
	}

	public override void OnRoundStart()
	{
		foreach ( var player in Scene.GetAllComponents<PlayerComponent>() )
		{
			player.Spawn();
		}
	}

	public void OnPlayerReady( PlayerComponent player )
	{
		player.Spawn();
	}
}
