using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adamantium.Engine.Animation
{
	public class CameraAnimationManager
	{
		public List<CameraCheckPoint> CameraCheckPointList { get; set; }
		public double RouteTime { get; set; }

		public int CurrentCheckPointNumber { get; set; }
		public int NextCheckPointNumber { get; set; }

		public CameraAnimationManager()
		{
			CameraCheckPointList = new List<CameraCheckPoint>();
		}

		public void ProcessList()
		{
			for(int i = 1; i < CameraCheckPointList.Count; ++i)
			{
				CurrentCheckPointNumber = i - 1;
				NextCheckPointNumber = i;
         }
		}
	}
}
