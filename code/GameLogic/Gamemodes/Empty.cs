using Sandbox;

[Hide]
[PhysboxGamemode( GameModes.None )]
public class EmptyGameMode : BaseGameMode
{
	public void OnPlayerDeath( PlayerComponent player, GameObject attacker )
	{
		// Respawn shortly.
		player.Invoke( PlayerConvars.RespawnTime, player.Spawn );
	}

	public override void OnRoundStart()
	{
		foreach ( var player in Scene.GetAllComponents<PlayerComponent>() )
		{
			player.Spawn();
		}
	}
}
