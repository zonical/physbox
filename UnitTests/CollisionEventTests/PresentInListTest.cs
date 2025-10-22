global using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox;
using System.Collections.Generic;

[TestClass]
public class PresentInClassTest
{
	[TestMethod]
	public void Test()
	{
		var scene = new Scene();
		using var sceneScope = scene.Push();

		var A = scene.CreateObject();
		var B = scene.CreateObject();
		Assert.IsNotNull( A );
		Assert.IsNotNull( B );

		var collisionA = new CollisionEvent( A, B );
		var collisionB = new CollisionEvent( B, A );
		Assert.IsNotNull( collisionA );
		Assert.IsNotNull( collisionB );

		var list = new List<CollisionEvent>();
		Assert.IsNotNull( list );

		list.Add( collisionA );
		Assert.IsTrue( list.Contains( collisionB ) );
	}
}
