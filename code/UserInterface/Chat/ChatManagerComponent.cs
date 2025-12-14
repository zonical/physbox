using Sandbox;
using System;
using Physbox;
using Sandbox.Audio;

public enum MessageType : int
{
	Player,
	System
}

[Group( "Physbox" )]
[Title( "Chat Manager" )]
[Icon( "directions_run" )]
[Tint( EditorTint.Yellow )]
[Hide]
public class ChatManagerComponent :
	Component, IPhysboxGameEvents,
	Component.INetworkListener
{
	[Property] [ActionGraphIgnore] public List<(MessageType, Guid, string)> Messages { get; set; } = new();

	protected override void OnEnabled()
	{
		// Delete ourselves if we're in the main menu.
		if ( PhysboxUtilities.IsMainMenuScene() )
		{
			DestroyGameObject();
		}
	}

	[Rpc.Broadcast]
	[ActionGraphIgnore]
	public void SendMessage( MessageType type, string text )
	{
		Messages.Add( (type, Rpc.CallerId, text) );
		Sound.Play( "sounds/player/chat_new_message.sound" );

		var chatDisplayComp = Game.ActiveScene.Get<ChatDisplay>();
		chatDisplayComp?.StateHasChanged();
	}

	[ConCmd( "pb_chat_test" )]
	public static void ChatTestMessage()
	{
		PhysboxUtilities.SendLocalChatMessage( MessageType.System, "This is a test message!" );
	}

	[ActionGraphIgnore]
	public void OnConnected( Connection channel )
	{
		SendMessage( MessageType.System, $"Player '{channel.DisplayName}' has joined the game." );
	}

	[ActionGraphIgnore]
	public void OnDisconnected( Connection channel )
	{
		SendMessage( MessageType.System, $"Player '{channel.DisplayName}' has left the game." );
	}

	[ActionGraphNode( "physbox.get_chat_instance" )]
	[Title( "Get Chat Manager" )]
	[Group( "Physbox" )]
	[Icon( "chat" )]
	public static ChatManagerComponent GetChatManager()
	{
		return Game.ActiveScene.Get<ChatManagerComponent>();
	}
}
