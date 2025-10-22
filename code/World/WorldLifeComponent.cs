namespace Sandbox;

[Group( "Physbox" )]
[Title( "GameObject (non-prop) Health" )]
[Icon( "favorite" )]
[Tint( EditorTint.Yellow )]
public class WorldLifeComponent : BaseLifeComponent
{
	[Property, Order( 0 )] public string Name { get; set; }

	protected override void OnStart()
	{
		Spawn();
	}

	public override void Spawn()
	{
		Health = MaxHealth;
	}

	[Rpc.Broadcast]
	public override void Die()
	{
		GameObject.Destroy();
	}
}
