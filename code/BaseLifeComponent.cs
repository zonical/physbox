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

	public void OnDamage( in DamageInfo damage )
	{
		if ( !IsAlive || DamageImmunity )
		{
			return;
		}

		var info = damage as PhysboxDamageInfo;
		if ( info is not null )
		{
			Health = int.Max( 0, Health - info.Damage );
			LastHitBy = info.Attacker;
		}
		// Some s&box components, such as TriggerHurt, will still
		// send us a regular DamageInfo struct.
		else
		{
			Health = int.Max( 0, Health - (int)damage.Damage );
		}

		// Oh no, we died! :(
		if ( Health == 0 )
		{
			DeathDamageInfo =
				info ?? new PhysboxDamageInfo { Damage = (int)damage.Damage, Victim = this as PlayerComponent };
			Die();
		}
	}

	[Rpc.Owner]
	public virtual void Spawn()
	{
		Health = MaxHealth;
		DeathDamageInfo = null;
	}

	[Rpc.Owner]
	public virtual void Die()
	{
	}
}
