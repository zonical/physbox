public interface IGameEvents : ISceneEvent<IGameEvents>
{
	// Called when a round has begun.
	void OnRoundStart() { }

	// Called when a round has ended. The game might not automatically restart when this is called.
	void OnRoundEnd() { }

	// Called when a player is spawned.
	void OnPlayerSpawn( GameObject player ) { }

	// Called when a player dies.
	void OnPlayerDeath( GameObject victim, DamageInfo info ) { }

	// Called when a player is fully connected and their player prefab has been created.
	void OnPlayerReady( GameObject player ) { }

	// Called when the score of a player has been updated.
	void OnPlayerScoreUpdate( GameObject player, int score ) { }
};
