using Physbox;
using Sandbox;

public class PhysboxDamageInfo : DamageInfo
{
	/// <summary>
	/// The player that attacked the victim.
	/// </summary>
	public new PlayerComponent Attacker { get; set; }

	/// <summary>
	/// The player that was attacked.
	/// </summary>
	public PlayerComponent Victim { get; set; }

	/// <summary>
	/// The prop that was used in the attack (if any).
	/// </summary>
	public PropDefinitionResource Prop { get; set; }

	/// <summary>
	/// Damage dealt from the attack.
	/// </summary>
	public new int Damage { get; set; }

	public bool IsWorld => Prop is null;
	public bool IsSuicide => Attacker == Victim;
}
