using System;
using System.Globalization;
using Sandbox;

public class TeamColorAttribute : Attribute
{
	public TeamColorAttribute( string color )
	{
		var parsedColor = uint.Parse( color.Replace( "#", "" ), NumberStyles.HexNumber );
		Color = Color.FromRgba( parsedColor );
	}

	public Color Color { get; private set; }
}

public enum Team
{
	[TeamColor( "#ffffffff" )] [Hide] None = -1,
	[TeamColor( "#d14949ff" )] Red = 0,
	[TeamColor( "#79a2e0ff" )] Blue = 1
}

public interface IPhysboxTeam
{
	Team Team { get; set; }
}
