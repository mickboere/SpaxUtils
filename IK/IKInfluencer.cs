using UnityEngine;

namespace SpaxUtils
{
	public struct IKInfluencer
	{
		public string Chain { get; set; }
		public Vector3 Position { get; set; }
		public float PositionWeight { get; set; }
		public Quaternion Rotation { get; set; }
		public float RotationWeight { get; set; }

		public IKInfluencer(string chain, Vector3 position, float positionWeight, Quaternion rotation, float rotationWeight)
		{
			Chain = chain;
			Position = position;
			Rotation = rotation;
			PositionWeight = positionWeight;
			RotationWeight = rotationWeight;
		}
	}
}
