using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Contains data for a combat hit scan.
	/// </summary>
	public struct HitScanHitData
	{
		public RaycastHit RaycastHit { get; private set; }
		public Vector3 Direction { get; private set; }

		public GameObject GameObject => RaycastHit.transform.gameObject;
		public Transform Transform => RaycastHit.transform;
		public Vector3 Point => RaycastHit.point;

		public HitScanHitData(RaycastHit hit, Vector3 direction)
		{
			RaycastHit = hit;
			Direction = direction;
		}
	}
}