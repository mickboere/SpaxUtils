using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Contains data for a combat hit scan.
	/// </summary>
	public struct HitScanHitData
	{
		public RaycastHit RaycastHit { get; private set; }

		/// <summary>
		/// The normalized direction of the scan at the moment of the hit.
		/// </summary>
		public Vector3 Direction { get; private set; }

		public GameObject GameObject => RaycastHit.transform.gameObject;
		public Transform Transform => RaycastHit.transform;
		public Vector3 Point => RaycastHit.point == Vector3.zero ? Origin : RaycastHit.point;
		public Vector3 Origin { get; private set; }

		public HitScanHitData(RaycastHit hit, Vector3 direction, Vector3 origin)
		{
			RaycastHit = hit;
			Direction = direction;
			Origin = origin;
		}
	}
}
