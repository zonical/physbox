public interface IGameEvents : ISceneEvent<IGameEvents>
{
	/// <summary>
	/// Called when a round has begun.
	/// </summary>
	void OnRoundStart() { }

	/// <summary>
	/// Called when a round has ended. The game might not automatically restart when this is called.
	/// </summary>
	void OnRoundEnd() { }

	/// <summary>
	/// Called when a player is spawned into the world.
	/// </summary>
	/// <param name="player">Player GameObject (has PlayerComponent attached).</param>
	void OnPlayerSpawn( GameObject player ) { }

	/// <summary>
	/// Called when a player dies.
	/// </summary>
	/// <param name="victim">Player GameObject (has PlayerComponent attached).</param>
	/// <param name="info">DamageInfo struct of how the player died.</param>
	void OnPlayerDeath( GameObject victim, DamageInfo info ) { }

	/// <summary>
	/// Called when the score of a player has been updated.
	/// </summary>
	/// <param name="player">Player GameObject (has PlayerComponent attached).</param>
	/// <param name="score">The total score for the player.</param>
	void OnPlayerScoreUpdate( GameObject player, int score ) { }
};
