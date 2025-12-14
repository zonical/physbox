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
	[Rpc.Owner]
	public void RequestSpawn()
	{
		Log.Info( $"RequestSpawn called for {Name}" );
		// If a spawn has already been requested, cancel it.
		if ( SpawnCancellationToken.CanBeCanceled )
		{
			SpawnCancellationTokenSource?.Cancel();
			SpawnCancellationTokenSource?.Dispose();
		}

		// Create a new token and spawn in X seconds.
		SpawnCancellationTokenSource = new CancellationTokenSource();
		SpawnCancellationToken = SpawnCancellationTokenSource.Token;
		Invoke( PlayerConvars.RespawnTime, Spawn, SpawnCancellationToken );
	}

	/// <summary>
	/// Spawns the player into the world.
	/// </summary>
	[Rpc.Owner]
	public override void Spawn()
	{
		base.Spawn();

		// Cancel anything that is trying to call Spawn() again,
		// e.g. getting respawned early due to the round restarting.
		if ( SpawnCancellationTokenSource is not null && !SpawnCancellationTokenSource.IsCancellationRequested )
		{
			SpawnCancellationTokenSource.Cancel();
		}

		SpawnCancellationTokenSource?.Dispose();
		SpawnCancellationToken = CancellationToken.None;

		if ( IsPlayer )
		{
			FreeCam = false;
			Hud?.StateHasChanged();
			Nametag?.StateHasChanged();

			// Spawn us slightly further up in the air so we don't get stuck in the ground.
			WorldPosition += Vector3.Up;
			Rigidbody.ApplyImpulse( Vector3.Up * 1000 );
		}

		ShowPlayer();
		BroadcastPutDownAnimation();
		DressPlayer();
		MovePlayerToSpawnpoint();

		// If we are not in a team at this pont, assign ourselves
		// to the team with the fewest players.
		if ( GameLogicComponent.UseTeams && Team == Team.None )
		{
			AssignToBestTeam();
		}

		Scene.RunEvent<IPhysboxGameEvents>( x => x.OnPlayerSpawn( this ) );
		Network.Refresh( this );
		Log.Info( $"PlayerComponent::Spawn() - {Name}" );
	}

	/// <summary>
	/// Oh no, the player has died!
	/// </summary>
	[Rpc.Owner]
	public override void Die()
	{
		CreateRagdoll();
		HidePlayer();
		DropObject();

		if ( IsPlayer )
		{
			FreeCam = true;
		}

		// If we are a bot, stop moving to our destination and forget everything.
		if ( IsBot && Components.TryGet<BotPlayerTasksComponent>( out var bot ) )
		{
			bot.Agent.Stop();

			bot.InterestedPlayer = null;
			bot.InterestedProp = null;
			bot.PickupAttemptsRemaining = bot.MaximumPickupAttempts;
			bot.ThrowAttemptsRemaining = bot.MaximumThrowAttempts;
		}

		// Let the game know we died.
		Scene.RunEvent<IPhysboxGameEvents>( x => x.OnPlayerDeath( DeathDamageInfo ) );
	}

	[Rpc.Broadcast]
	private void HidePlayer()
	{
		Collider.Enabled = false;
		Renderer.Enabled = false;
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
		Collider.Enabled = true;
		Renderer.Enabled = true;
		Nametag.Enabled = true;
	}

	public new void OnDamage( in DamageInfo damage )
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

	[Rpc.Broadcast]
	public void VisualiseDamageImmunity()
	{
		foreach ( var model in Components.GetAll<ModelRenderer>() )
		{
			if ( model.Tags.Contains( PhysboxConstants.HeldPropTag ) )
			{
				continue;
			}

			var color = Color.White;
			Color immuneColor = "#8eeddf";
			var isTeamClothing = model.Model.Name.Contains( "loose_jumper" ) ||
			                     model.Model.Name.Contains( "trackie_bottoms" );

			// Tint us slightly transparent.
			if ( DamageImmunity )
			{
				if ( isTeamClothing )
				{
					color = GameLogicComponent.UseTeams ? PhysboxUtilities.GetTeamColor( Team ) : Color.White;
					color = color.WithAlpha( 0.5f );
				}
				else
				{
					color = immuneColor.WithAlpha( 0.5f );
				}
			}
			else
			{
				// If we are clothing, revert back to our original colour.
				if ( isTeamClothing )
				{
					color = GameLogicComponent.UseTeams ? PhysboxUtilities.GetTeamColor( Team ) : Color.White;
				}
			}

			model.Tint = color;
			model.Network.Refresh();
		}
	}
}
