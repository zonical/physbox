using Sandbox;
using System;
using System.Threading;

public partial class PlayerComponent
{
	public CancellationTokenSource SpawnCancellationTokenSource;
	public CancellationToken SpawnCancellationToken = CancellationToken.None;

	/// <summary>
	/// Attatches a cancellation token to a spawn request.
	/// </summary>
	public void RequestSpawn()
	{
		SpawnCancellationTokenSource = new CancellationTokenSource();
		SpawnCancellationToken = SpawnCancellationTokenSource.Token;

		Invoke( PlayerConvars.RespawnTime, Spawn, SpawnCancellationToken );
	}

	/// <summary>
	/// Spawns the player into the world.
	/// </summary>
	[Rpc.Owner( NetFlags.OwnerOnly | NetFlags.SendImmediate )]
	public override void Spawn()
	{
		// Cancel anything that is trying to call Spawn() again,
		// e.g. getting respawned early due to the round restarting.
		if ( SpawnCancellationTokenSource is not null && !SpawnCancellationTokenSource.IsCancellationRequested )
		{
			SpawnCancellationTokenSource.Cancel();
		}

		SpawnCancellationTokenSource?.Dispose();
		SpawnCancellationToken = CancellationToken.None;

		base.Spawn();

		// Revive and reset the player.
		Health = 100;
		Hitbox.Enabled = true;

		// If we are not in a team, assign ourselves to the team with the fewest players.
		if ( GameLogicComponent.UseTeams && Team == Team.None )
		{
			var teams = Scene.GetAllComponents<PlayerComponent>()
				.GroupBy( x => x.Team )
				.Where( x => x.Key != Team.None );

			Team = teams.OrderBy( x => x.Count() ).First().Key;
		}

		ShowPlayer();
		MovePlayerToSpawnpoint();
		DressPlayer();

		if ( IsPlayer )
		{
			FreeCam = false;
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

	/// <summary>
	/// Oh no, the player has died!
	/// </summary>
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
		Scene.RunEvent<IGameEvents>( x => x.OnPlayerDeath( DeathDamageInfo ) );
	}

	[Rpc.Broadcast]
	private void HidePlayer()
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
	private void ShowPlayer()
	{
		PlayerController.Renderer.Enabled = true;
		PlayerController.ColliderObject.Enabled = true;
		Nametag.Enabled = true;
	}

	public override void OnDamage( in DamageInfo damage )
	{
		if ( IsProxy || GodMode )
		{
			return;
		}

		base.OnDamage( damage );
	}

	public void CommitSuicide()
	{
		OnDamage( new PhysboxDamageInfo { Damage = 9999, Prop = null, Victim = this, Attacker = this } );
	}
}
