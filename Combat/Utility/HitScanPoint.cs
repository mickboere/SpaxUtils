using UnityEngine;

namespace SpaxUtils
{
	public struct HitScanPoint
	{
		public Vector3 Position { get; set; }
		public Quaternion Rotation { get; set; }
		public Vector3 Center { get; set; }
		public Vector3 Forward => Rotation * Vector3.forward;
		public Vector3 Up => Rotation * Vector3.up;
		public Vector3 Right => Rotation * Vector3.right;
	}
}