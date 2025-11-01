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
}
