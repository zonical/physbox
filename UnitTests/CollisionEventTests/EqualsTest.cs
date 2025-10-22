global using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox;

[TestClass]
public class EqualsTest
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

		Assert.IsTrue( collisionA == collisionB );

		var C = scene.CreateObject();
		Assert.IsNotNull( C );

		var collisionC = new CollisionEvent( C, B );
		Assert.IsNotNull( collisionC );
		Assert.IsFalse( collisionC == collisionB );
	}
}
