using Adamantium.Mathematics;

namespace Adamantium.Engine.Animation
{
	public class CameraCheckPoint
	{
		public Vector3F DestinationPosition { get; set; }
		public QuaternionF DestinationRotation { get; set; }
		public double TimeToArrive { get; set; }
		public CameraCheckPoint(Vector3F destPosition, QuaternionF destRotation, double timeToArrive)
		{
			DestinationPosition = destPosition;
			DestinationRotation = destRotation;
			TimeToArrive = timeToArrive;
		}
	}
}