using Sandbox;
using Sandbox.Audio;

public partial class GameLogicComponent
{
	// All the timer sounds avaliable for seconds.
	private readonly Dictionary<int, string> AnnouncerSecondsSounds = new()
	{
		{ 60, "sounds/announcer/60-seconds.sound" },
		{ 30, "sounds/announcer/30-seconds.sound" },
		{ 10, "sounds/announcer/10-seconds.sound" },
		{ 5, "sounds/announcer/countdown-5.sound" },
		{ 4, "sounds/announcer/countdown-4.sound" },
		{ 3, "sounds/announcer/countdown-3.sound" },
		{ 2, "sounds/announcer/countdown-2.sound" },
		{ 1, "sounds/announcer/countdown-1.sound" }
	};

	// Sounds that have already been broadcast. This is here so we don't
	// play one sound fifty million times.
	private List<int> AnnouncerSoundsAlreadyPlayed = new();

	/// <summary>
	/// Called every fixed update.
	/// </summary>
	private void AnnouncerUpdate()
	{
		// Play announcer sound.
		if ( AnnouncerSecondsSounds.TryGetValue( TimeLeft, out var sound ) &&
		     !AnnouncerSoundsAlreadyPlayed.Contains( TimeLeft ) )
		{
			AnnouncerSoundsAlreadyPlayed.Add( TimeLeft );
			Sound.Play( sound, Mixer.FindMixerByName( "UI" ) );
		}
	}
}
