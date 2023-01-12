using UnityEngine;

namespace SpaxUtils
{
	public struct IKInfluencer
	{
		public string Chain { get; set; }
		public int Priority { get; set; }
		public Vector3 Position { get; set; }
		public float PositionWeight { get; set; }
		public Quaternion Rotation { get; set; }
		public float RotationWeight { get; set; }

		public IKInfluencer(string chain, int priority, Vector3 position, float positionWeight, Quaternion rotation, float rotationWeight)
		{
			Chain = chain;
			Priority = priority;
			Position = position;
			Rotation = rotation;
			PositionWeight = positionWeight;
			RotationWeight = rotationWeight;
		}
	}
}
