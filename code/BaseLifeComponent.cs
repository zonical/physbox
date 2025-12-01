using Sandbox;


[Hide]
public class BaseLifeComponent : Component, Component.IDamageable
{
	[Feature( "Life" )]
	[Property]
	[Order( 100 )]
	[Sync]
	public int Health { get; set; }

	[Feature( "Life" )]
	[Property]
	[Order( 101 )]
	[Sync]
	public int MaxHealth { get; set; } = 100;

	[Feature( "Life" )]
	[Property]
	[Order( 102 )]
	[Sync]
	public PlayerComponent LastHitBy { get; set; }

	[Feature( "Life" )]
	[Property]
	[Order( 103 )]
	[Sync]
	public bool DamageImmunity { get; set; } = false;

	[Feature( "Life" )]
	[Property]
	[ReadOnly]
	[Order( 104 )]
	public PhysboxDamageInfo DeathDamageInfo { get; set; } = null;

	public bool IsAlive => Health > 0;

	[Rpc.Owner]
	public void RequestDamage( PhysboxDamageInfo damage )
	{
		OnDamage( damage );
	}

	public virtual void OnDamage( in DamageInfo damage )
	{
		if ( !IsAlive )
		{
			return;
		}

		if ( DamageImmunity )
		{
			return;
		}

		if ( damage is PhysboxDamageInfo info )
		{
			Log.Info( $"{this} received {info.Damage} damage" );
			Health = int.Max( 0, Health - info.Damage );
			LastHitBy = info.Attacker;

			// Oh no, we died! :(
			if ( Health == 0 )
			{
				DeathDamageInfo = info;
				Die();
			}
		}
	}

	public virtual void Spawn()
	{
		Health = MaxHealth;
		DeathDamageInfo = null;
	}

	public virtual void Die()
	{
	}
}
