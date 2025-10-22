global using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox;

[TestClass]
public class TestInit
{
	[AssemblyInitialize]
	public static void ClassInitialize( TestContext context )
	{
		Sandbox.Application.InitUnitTest<TestInit>( false );
	}

	[AssemblyCleanup]
	public static void AssemblyCleanup()
	{
		Sandbox.Application.ShutdownUnitTest();
	}
}
