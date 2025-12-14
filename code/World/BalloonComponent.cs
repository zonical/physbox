namespace Sandbox;

public class BalloonComponent : Component
{
	[Property] public GameObject AttachmentPoint;

	protected override void OnUpdate()
	{
		if ( AttachmentPoint is null )
		{
			return;
		}

		WorldTransform = AttachmentPoint.WorldTransform;
	}
}
