using Sandbox;
using System.Numerics;
using System.Threading.Channels;

public static partial class PhysboxDebug
{
	[ConCmd( "pb_bot_create", ConVarFlags.Cheat )]
	public static void DebugBotCreate( Connection caller )
	{
		if ( !caller.IsHost )
		{
			return;
		}

		var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/bot.prefab" );
		if ( prefab is null )
		{
			Log.Error( "Could not find prefab file." );
			return;
		}

		// Spawn this object and make the client the owner.
		var prefabScene = SceneUtility.GetPrefabScene( prefab );
		var go = prefabScene.Clone( new Transform(), name: $"BOT (Placeholder)" );
		go.BreakFromPrefab();
		go.NetworkSpawn( caller );
	}

	[ConCmd( "pb_bot_suicide", ConVarFlags.Cheat )]
	public static void DebugBotSuicide( Connection caller )
	{
		if ( !caller.IsHost )
		{
			return;
		}

		foreach ( var bot in Game.ActiveScene.GetAllComponents<PlayerComponent>().Where( x => x.IsBot ) )
		{
			bot.CommitSuicide();
		}
	}
}
