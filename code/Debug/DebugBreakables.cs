using Physbox;

public static partial class PhysboxDebug
{
	[ConVar( "pb_debug_thrower_preview" )] public static bool DebugThrowerPreview { get; set; } = false;

	[ConCmd( "pb_debug_create_breakable" )]
	public static void DebugCreateBreakableProp( Connection caller, string prop = "crate" )
	{
		var player = PlayerComponent.LocalPlayer;

		// Find caller in the world.
		if ( player is null )
		{
			Log.Error( "Could not find local player." );
			return;
		}

		// Create a prop in front of us.
		if ( prop.EndsWith( ".pdef" ) )
		{
			prop = prop[..^5];
		}

		if ( prop.StartsWith( "props/" ) )
		{
			prop = prop[5..];
		}

		var go = PhysboxUtilities.CreatePropFromResource(
			ResourceLibrary.Get<PropDefinitionResource>( $"props/{prop}.pdef" ) );

		go.WorldPosition = player.Camera.WorldPosition + player.Camera.WorldRotation.Forward * 100;
		go.WorldRotation = player.Camera.WorldRotation;
	}

	[ConCmd( "pb_voice_loopback" )]
	public static void DebugLoopback( Connection caller )
	{
		var player = PlayerComponent.LocalPlayer;

		// Find caller in the world.
		if ( player is null )
		{
			Log.Error( "Could not find local player." );
			return;
		}

		player.Voice.Loopback = !player.Voice.Loopback;
		Log.Info( $"Loopback set to {player.Voice.Loopback}." );
	}

	[ConCmd( "pb_debug_held_pose" )]
	public static void DebugPose( Connection caller, int pose )
	{
		var player = PlayerComponent.LocalPlayer;

		// Find caller in the world.
		if ( player is null )
		{
			Log.Error( "Could not find local player." );
			return;
		}

		player.PlayerController.Renderer.Parameters.Set( "holdtype_pose", pose );
	}

	[ConCmd( "pb_debug_modify_kills", ConVarFlags.Cheat )]
	public static void ModifyKills( Connection caller, int kills )
	{
		var player = PlayerComponent.LocalPlayer;

		// Find caller in the world.
		if ( player is null )
		{
			Log.Error( "Could not find local player." );
			return;
		}

		player.Kills += kills;
		player.Scene.RunEvent<IPhysboxGameEvents>( x => x.OnPlayerScoreUpdate( player, player.Kills ) );
	}

	[ConCmd( "pb_debug_break_held_prop", ConVarFlags.Cheat )]
	public static void BreakHeldProp( Connection caller )
	{
		var player = PlayerComponent.LocalPlayer;

		// Find caller in the world.
		if ( player is null )
		{
			Log.Error( "Could not find local player." );
			return;
		}

		var life = player.HeldGameObject?.GetComponent<PropLifeComponent>();
		life?.Die();
	}
}
