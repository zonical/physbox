public interface IGameEvents : ISceneEvent<IGameEvents>
{
	// Called when a round has begun.
	void OnRoundStart() { }

	// Called when a round has ended. The game might not automatically restart when this is called.
	void OnRoundEnd() { }

	// Called when a player is spawned.
	void OnPlayerSpawn( PlayerComponent player ) { }

	// Called when a player dies.
	void OnPlayerDeath( PlayerComponent player, GameObject attacker ) { }

	// Called when a player is fully connected and their player prefab has been created.
	void OnPlayerReady( PlayerComponent player ) { }

	// Called when the score of a player has been updated.
	void OnPlayerScoreUpdate( PlayerComponent player, int score ) { }
};
