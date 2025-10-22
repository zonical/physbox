using Sandbox;
using System;
using System.Threading;

public partial class PlayerComponent
{
	public CancellationTokenSource SpawnCancellationTokenSource;
	public CancellationToken SpawnCancellationToken = CancellationToken.None;

	public void RequestSpawn()
	{
		SpawnCancellationTokenSource = new CancellationTokenSource();
		SpawnCancellationToken = SpawnCancellationTokenSource.Token;

		Invoke( PlayerConvars.RespawnTime, Spawn, SpawnCancellationToken );
	}

	// Spawns the player into the world.
	[Rpc.Owner( NetFlags.OwnerOnly | NetFlags.SendImmediate )]
	public override void Spawn()
	{
		// Cancel anything that is trying to call Spawn() again,
		// e.g. getting respawned early due to the round restarting.
		if ( !SpawnCancellationTokenSource.IsCancellationRequested )
		{
			SpawnCancellationTokenSource.Cancel();
		}

		SpawnCancellationTokenSource.Dispose();
		SpawnCancellationToken = CancellationToken.None;

		base.Spawn();

		// Revive and reset the player.
		Health = 100;
		FreeCam = false;
		ShowPlayer();
		DressPlayer();
		Hitbox.Enabled = true;

		// Teleport to spawnpoint.
		var regularSpawnpoint = Game.Random.FromList( Scene.GetAllComponents<PhysboxSpawnpoint>().ToList() );
		if ( regularSpawnpoint is not null )
		{
			WorldPosition = regularSpawnpoint.WorldPosition;
			WorldRotation = regularSpawnpoint.WorldRotation;
		}

		// Delete our ragdoll.
		if ( Ragdoll is not null )
		{
			Ragdoll.Destroy();
			Ragdoll = null;
		}

		// Enable temporary damage immunity.
		DamageImmunity = true;
		Invoke( PlayerConvars.RespawnImmunity, () => { DamageImmunity = false; } );
	}

	// Kills the player in the world.
	[Rpc.Owner( NetFlags.OwnerOnly )]
	public override void Die()
	{
		FreeCam = true;

		CreateRagdoll();
		HidePlayer();
		Hitbox.Enabled = false;

		// Let the game know we died.
		Scene.RunEvent<IGameEvents>( x => x.OnPlayerDeath( this, DeathDamageInfo ) );
	}

	[Rpc.Broadcast]
	public void HidePlayer()
	{
		PlayerController.Renderer.Enabled = false;
		PlayerController.ColliderObject.Enabled = false;
		Nametag.Enabled = false;
	}

	[Rpc.Broadcast]
	public void ShowPlayer()
	{
		PlayerController.Renderer.Enabled = true;
		PlayerController.ColliderObject.Enabled = true;
		Nametag.Enabled = true;
	}
}
