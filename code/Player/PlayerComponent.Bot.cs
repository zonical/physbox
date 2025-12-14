using System;
using Sandbox;
using Sandbox.Citizen;
using Networking = Sandbox.Debug.Networking;

public partial class PlayerComponent
{
	/// <summary>
	/// Bot update loop. Very simple.
	/// </summary>
	private void OnBotUpdate()
	{
		if ( !IsAlive )
		{
			return;
		}

		if ( HeldGameObject is not null && HeldProp is not null )
		{
			PositionHeldObject();
		}
	}

	/// <summary>
	/// Assigns a random name to a bot.
	/// </summary>
	private void AssignBotName()
	{
		// TODO: Prevent repeating names.
		var names = new List<string>
		{
			"Adam",
			"Rhys",
			"Chloe",
			"Jasmine",
			"Jo",
			"Jack",
			"Maverick",
			"Ruth",
			"Paul",
			"Patrick",
			"Xavier",
			"Bridget",
			"Bianca",
			"Josh",
			"Max",
			"Lochie",
			"Suzie",
			"Philip",
			"Tahlia",
			"Leo",
			"Francois",
			"Rowan",
			"Elise",
			"Robert",
			"Jono"
		};

		BotName = "[BOT] " + Game.Random.FromList( names );
		Nametag.Name = BotName;
		GameObject.Name = BotName;
	}

	/// <summary>
	/// Shamelessly stolen from the s&box documentation.
	/// </summary>
	private async void OnBotLinkJump()
	{
		RendererComponent.Set( "b_grounded", false );
		RendererComponent.Set( "b_jump", true );

		var start = BotAgent.CurrentLinkTraversal.Value.AgentInitialPosition;
		var end = BotAgent.CurrentLinkTraversal.Value.LinkExitPosition;

		BotAgent.SetAgentPosition( end );
		BotAgent.CompleteLinkTraversal();

		RendererComponent.Set( "b_grounded", true );
		RendererComponent.Set( "b_jump", false );
	}
}
