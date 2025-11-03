using Sandbox;
using Physbox;

public static class PhysboxUtilites
{
	public static GameObject CreatePropFromResource( PropDefinitionResource resource )
	{
		//Log.Info( $"Creating prop {resource.ResourcePath}" );
		var GameObject = new GameObject( true, "Breakable Prop" );
		GameObject.Tags.Add( PhysboxConstants.BreakablePropTag );

		// Create the necessary components.
		var modelRenderer = GameObject.AddComponent<ModelRenderer>();
		var modelCollider = GameObject.AddComponent<ModelCollider>();
		var rigidBody = GameObject.AddComponent<Rigidbody>();
		var life = GameObject.AddComponent<PropLifeComponent>();
		GameObject.AddComponent<ObjectCollisionListenerComponent>();

		// Set our resource (this gets converted to PropDefinitionResource on the game side).
		var defComp = GameObject.AddComponent<PropDefinitionComponent>();
		defComp.Definition = resource;

		// Apply models.
		var model = resource.Model;
		modelRenderer.Model = model;
		modelCollider.Model = model;

		// Apply our mass.
		var mass = resource.Mass;
		rigidBody.MassOverride = mass;

		// Apply our health.
		var maxHealth = resource.MaxHealth;
		life.MaxHealth = maxHealth;

		// Update our name.
		var name = resource.Name;
		GameObject.Name = $"Breakable Prop ({name})";
		//GameObject.MakeNameUnique();

		GameObject.NetworkMode = NetworkMode.Object;
		GameObject.Network.SetOwnerTransfer( OwnerTransfer.Takeover );
		GameObject.NetworkSpawn();

		return GameObject;
	}

	[Rpc.Broadcast]
	public static void IncrementStatRPC( string stat, int value )
	{
		Sandbox.Services.Stats.Increment( stat, value );
	}

	/// <summary>
	/// Increments a stat on the s&box backend. We use this as a convenient wrapper to ensure that
	/// stats are going to the right connections. If I was a good networking programmer, I wouldn't
	/// have to worry about this, but I want to be safe.
	/// </summary>
	/// <param name="player"></param>
	/// <param name="stat"></param>
	/// <param name="value"></param>
	public static void IncrementStatForPlayer( PlayerComponent player, string stat, int value )
	{
		if ( player.IsBot ) return;

		using ( Rpc.FilterInclude( c => c.Id == player.Network.Owner.Id ) )
		{
			IncrementStatRPC( stat, value );
		}
	}
}
