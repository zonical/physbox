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
		if ( SpawnCancellationTokenSource is not null && !SpawnCancellationTokenSource.IsCancellationRequested )
		{
			SpawnCancellationTokenSource.Cancel();
		}

		SpawnCancellationTokenSource.Dispose();
		SpawnCancellationToken = CancellationToken.None;

		base.Spawn();

		// Revive and reset the player.
		Health = 100;
		ShowPlayer();
		Hitbox.Enabled = true;

		var spawnpoint = (GameObject)null;

		// Prioritise Physbox spawnpoints first.
		spawnpoint = Game.Random.FromList( Scene.GetAllComponents<PhysboxSpawnpoint>().ToList() )?.GameObject;
		if ( spawnpoint is null )
		{
			// Then find a normal Sandbox one.
			spawnpoint = Game.Random.FromList( Scene.GetAllComponents<SpawnPoint>().ToList() )?.GameObject;
		}

		// Teleport to spawnpoint.
		if ( spawnpoint is not null )
		{
			WorldPosition = spawnpoint.WorldPosition;
			PlayerController.EyeAngles = spawnpoint.WorldRotation;

			// If we are a bot, force set our destination.
			if ( IsBot && Components.TryGet<BotPlayerTasksComponent>( out var bot ) )
			{
				bot.Agent.SetAgentPosition( WorldPosition );
			}
		}

		if ( IsPlayer )
		{
			FreeCam = false;
			DressPlayer();

			PlayerController.Jump( Vector3.Up );
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
		if ( IsPlayer )
		{
			FreeCam = true;
		}

		CreateRagdoll();
		HidePlayer();
		DropObject();

		// If we are a bot, stop moving to our destination and forget everything.
		if ( IsBot && Components.TryGet<BotPlayerTasksComponent>( out var bot ) )
		{
			bot.Agent.Stop();

			bot.InterestedPlayer = null;
			bot.InterestedProp = null;
			bot.PickupAttemptsRemaining = bot.MaximumPickupAttempts;
			bot.ThrowAttemptsRemaining = bot.MaximumThrowAttempts;
		}

		Hitbox.Enabled = false;

		// Let the game know we died.
		Scene.RunEvent<IGameEvents>( x => x.OnPlayerDeath( GameObject, DeathDamageInfo ) );
	}

	[Rpc.Broadcast]
	public void HidePlayer()
	{
		PlayerController.Renderer.Enabled = false;
		PlayerController.ColliderObject.Enabled = false;
		Nametag.Enabled = false;

		// Hide all clothes (or things attached to us)
		foreach ( var ren in Components.GetAll<ModelRenderer>(
			         FindMode.Enabled | FindMode.InChildren | FindMode.InDescendants ) )
		{
			if ( ren.Tags.Contains( "viewmodel" ) )
			{
				continue;
			}

			ren.Enabled = false;
		}
	}

	[Rpc.Broadcast]
	public void ShowPlayer()
	{
		PlayerController.Renderer.Enabled = true;
		PlayerController.ColliderObject.Enabled = true;
		Nametag.Enabled = true;
	}

	public override void OnDamage( in DamageInfo damage )
	{
		if ( IsProxy )
		{
			return;
		}

		if ( GodMode )
		{
			return;
		}

		base.OnDamage( damage );
	}
}
