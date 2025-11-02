using Sandbox;
using System;

public enum MessageType : int
{
	Player,
	System
}

[Hide]
public class ChatManagerComponent : Component, IGameEvents, Component.INetworkListener
{
	[Property] public List<(MessageType, Guid, string)> Messages { get; set; } = new();

	[Rpc.Broadcast]
	public void SendMessage( MessageType type, string text )
	{
		Messages.Add( (type, Rpc.CallerId, text) );
	}

	[ConCmd( "pb_chat_test" )]
	public static void ChatTestMessage()
	{
		var manager = Game.ActiveScene.Get<ChatManagerComponent>();
		manager.SendMessage( MessageType.System, "This is a test message!" );
	}

	public void OnConnected( Connection channel )
	{
		SendMessage( MessageType.System, $"Player '{channel.DisplayName}' has joined the game." );
	}

	public void OnDisconnected( Connection channel )
	{

		SendMessage( MessageType.System, $"Player '{channel.DisplayName}' has left the game." );
	}

}
