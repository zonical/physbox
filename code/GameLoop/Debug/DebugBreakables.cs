using Sandbox;

public static partial class PhysboxDebug
{
	[ConCmd( "pb_debug_create_breakable" )]
	public static void DebugCreateBreakableProp( Connection caller )
	{
		var player = PlayerComponent.LocalPlayer;

		// Find caller in the world.
		if ( player is null )
		{
			Log.Error( "Could not find local player." );
			return;
		}

		// Create a prop in front of us.
		var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/breakable_prop.prefab" );
		if ( prefab is null )
		{
			Log.Error( "Could not find prefab file." );
			return;
		}

		var prefabScene = SceneUtility.GetPrefabScene( prefab );
		var go = prefabScene.Clone();
		go.NetworkSpawn();

		go.WorldPosition = player.Camera.WorldPosition + (player.Camera.WorldRotation.Forward * 100);
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
	public static void DebugePose( Connection caller, int pose )
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

	[ConCmd( "pb_debug_modify_kills" )]
	public static void Modifykills( Connection caller, int kills )
	{
		var player = PlayerComponent.LocalPlayer;

		// Find caller in the world.
		if ( player is null )
		{
			Log.Error( "Could not find local player." );
			return;
		}

		player.Kills += kills;
		player.Scene.RunEvent<IGameEvents>( x => x.OnPlayerScoreUpdate( player.GameObject, player.Kills ) );
	}

	[ConVar( "pb_debug_thrower_preview" )]
	public static bool DebugThrowerPreview { get; set; } = false;
}
