using Sandbox;


[Hide]
public class BaseLifeComponent : Component, Component.IDamageable
{
	[Feature( "Life" ), Property, Order( 100 ), ReadOnly, Sync]
	public int Health { get; set; }

	[Feature( "Life" ), Property, Order( 101 ), Sync]
	public int MaxHealth { get; set; } = 100;

	[Feature( "Life" ), Property, Order( 102 ), Sync]
	public GameObject LastHitBy { get; set; }

	[Feature( "Life" ), Property, Order( 103 ), Sync]
	public bool DamageImmunity { get; set; } = false;

	[Feature( "Life" ), Property, ReadOnly, Order( 104 )]
	public DamageInfo DeathDamageInfo { get; set; } = null;

	public bool IsAlive => Health > 0;

	protected override void OnStart()
	{
	}

	[Rpc.Owner]
	public void RequestDamage( DamageInfo damage )
	{
		OnDamage( damage );
	}

	public virtual void OnDamage( in DamageInfo damage )
	{
		if ( !IsAlive ) return;
		if ( DamageImmunity ) return;

		Health = int.Max( 0, Health - (int)damage.Damage );
		LastHitBy = damage.Attacker;

		// Oh no, we died! :(
		if ( Health == 0 )
		{
			DeathDamageInfo = damage;
			Die();
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
