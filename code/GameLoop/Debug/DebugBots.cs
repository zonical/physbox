using Sandbox;
using System.Numerics;
using System.Threading.Channels;

public static partial class PhysboxDebug
{
	[ConCmd( "pb_bot_create" )]
	public static void DebugBotCreate( Connection caller )
	{
		if ( !caller.IsHost ) return;

		var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/bot.prefab" );
		if ( prefab is null )
		{
			Log.Error( "Could not find prefab file." );
			return;
		}

		// Spawn this object and make the client the owner.
		var prefabScene = SceneUtility.GetPrefabScene( prefab );
		var go = prefabScene.Clone( new(), name: $"BOT (Placeholder)" );
		go.BreakFromPrefab();
		go.NetworkSpawn( caller );

		// Once the player has been spawned on the network, we can go ahead and
		// initalise them. Doing this in OnNetworkSpawn is too early.
		var player = go.GetComponent<PlayerComponent>();
		player.InitBot();
	}

	[ConCmd( "pb_bot_suicide" )]
	public static void DebugBotSuicide( Connection caller )
	{
		if ( !caller.IsHost ) return;

		foreach ( var bot in Game.ActiveScene.GetAllComponents<PlayerComponent>().Where( x => x.IsBot ) ) 
		{
			var damageinfo = new DamageInfo( 9999, bot.GameObject, null );
			bot.OnDamage( damageinfo );
		}
	}
}
