using System;
using Sandbox;
using Networking = Sandbox.Debug.Networking;

public partial class PlayerComponent :
	BaseLifeComponent,
	IGameEvents,
	PlayerController.IEvents
{
	/// <summary>
	/// Initialises a bot player. Thankfully, there is a lot less to do
	/// compared to a normal player!
	/// </summary>
	[Rpc.Owner]
	public void InitBot()
	{
		AssignBotName();
		Networking.AddEmptyConnection();
		HidePlayerControllerComponent();
		HidePlayer();
		CreateHitbox();

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

		if ( HeldGameObject is not null && HeldProp is not null )
		{
			PositionHeldObject();
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

		// Calculate peak height for the parabolic arc
		var heightDifference = end.z - start.z;
		var peakHeight = MathF.Abs( heightDifference ) + 25f;

		var mid = (start + end) / 2f;

		// Estimate prabolic duration size using a triangle /\ between start, mid, end 
		var startToMid = mid.WithZ( peakHeight ) - start;
		var midToEnd = end - mid.WithZ( peakHeight );
		var duration = (startToMid + midToEnd).Length / BotAgent.MaxSpeed;
		duration = MathF.Max( 0.75f, duration ); // Ensure minimum duration

		TimeSince timeSinceStart = 0;

		while ( timeSinceStart < duration )
		{
			var t = timeSinceStart / duration;

			// Linearly interpolate XY positions
			var newPosition = Vector3.Lerp( start, end, t );

			// Apply parabolic curve to Z position using a quadratic function
			var yOffset = 4f * peakHeight * t * (1f - t);
			newPosition.z = MathX.Lerp( start.z, end.z, t ) + yOffset;

			BotAgent.SetAgentPosition( newPosition );

			await Task.Frame();
		}

		BotAgent.SetAgentPosition( end );
		BotAgent.CompleteLinkTraversal();
	}
}
