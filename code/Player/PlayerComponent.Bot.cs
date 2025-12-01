using System;
using Sandbox;
using Sandbox.Citizen;
using Networking = Sandbox.Debug.Networking;

public partial class PlayerComponent
{
	/// <summary>
	/// Initialises a bot player. Thankfully, there is a lot less to do
	/// compared to a normal player!
	/// </summary>
	[Rpc.Owner]
	public void InitBot()
	{
		AssignBotName();
		DressPlayer();
		HidePlayerControllerComponent();
		HidePlayer();
		CreateHitbox();
		Networking.AddEmptyConnection();

		// Make our agent move very quickly.
		BotAgent.Acceleration = PlayerConvars.RunSpeed;
		BotAgent.MaxSpeed = PlayerConvars.RunSpeed;

		var game = GameLogicComponent.GetGameInstance();
		if ( !game.RoundOver )
		{
			RequestSpawn();
		}
	}

	/// <summary>
	/// Bot update loop. Very simple.
	/// </summary>
	private void OnBotUpdate()
	{
		if ( !IsAlive )
		{
			return;
		}

		// HACK: This is fucking terrible. For some godforsaken reason,
		// the bots do not place nice with PositionHeldObject and HeldProp.
		// I've tried everything I can think of, but HeldProp just randomly
		// goes null sometimes and throws an exception. It's probably something
		// to do with the prop pickup code, but I don't even know where to begin
		// to look. Maybe when I am smarter, I can get rid of this stupid
		// exception blocker.
		try
		{
			if ( HeldGameObject is not null && HeldProp is not null )
			{
				PositionHeldObject();
			}
		}
		catch
		{
			return;
		}
	}

	/// <summary>
	/// If we have the PlayerController component for what ever reason,
	/// disable it. We only have one that we locally control.
	/// </summary>
	private void HidePlayerControllerComponent()
	{
		var playerController = GetComponent<PlayerController>();
		if ( playerController is null )
		{
			return;
		}

		playerController.Enabled = false;

		// Delete all of our existing colliders.
		foreach ( var collider in Components.GetAll<Collider>(
			         FindMode.EverythingInSelf |
			         FindMode.EverythingInAncestors |
			         FindMode.EverythingInDescendants ) )
		{
			collider.Destroy();
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
		Renderer.Set( "b_grounded", false );
		Renderer.Set( "b_jump", true );

		var start = BotAgent.CurrentLinkTraversal.Value.AgentInitialPosition;
		var end = BotAgent.CurrentLinkTraversal.Value.LinkExitPosition;

		BotAgent.SetAgentPosition( end );
		BotAgent.CompleteLinkTraversal();

		Renderer.Set( "b_grounded", true );
		Renderer.Set( "b_jump", false );
	}
}
