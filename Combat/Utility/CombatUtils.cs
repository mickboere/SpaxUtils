using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oddworm.Framework;

namespace SpaxUtils
{
	/// <summary>
	/// Utility functions for combat.
	/// </summary>
	public static class CombatUtils
	{
		public static bool DEBUG = false;
		public static float DEBUG_DURATION = 6f;

		#region Hit Scans

		/// <summary>
		/// Performs a hit scan along <paramref name="scanPoints"/>, invoking <paramref name="scanFunc"/> between each.
		/// </summary>
		/// <param name="scanPoints">The path of <see cref="HitScanPoint"/>s to iterate through.</param>
		/// <param name="scanFunc">Func invoked between each scan point, called using
		/// (<see cref="HitScanPoint"/> current, <see cref="HitScanPoint"/> next, <see cref="Vector3"/> toNext)
		/// and should return an array of <see cref="RaycastHit"/>s.</param>
		/// <returns>Collection of <see cref="HitScanHitData"/> containing all unique hits encountered along the scan.</returns>
		public static List<HitScanHitData> HitScan(
			List<HitScanPoint> scanPoints,
			Func<HitScanPoint, HitScanPoint, Vector3, RaycastHit[]> scanFunc)
		{
			List<HitScanHitData> hits = new List<HitScanHitData>();
			for (int i = 0; i < scanPoints.Count - 1; i++)
			{
				HitScanPoint current = scanPoints[i];
				HitScanPoint next = scanPoints[i + 1];

				Vector3 toNext = next.Center - current.Center;
				RaycastHit[] scanHits = scanFunc(current, next, toNext);
#if UNITY_EDITOR
				if (DEBUG)
				{
					Color color = Color.HSVToRGB(UnityEngine.Random.value, 1f, 1f);
					Debug.DrawLine(current.Center, next.Center, color, DEBUG_DURATION * Time.timeScale);
				}
#endif
				if (scanHits != null)
				{
					foreach (RaycastHit hit in scanHits)
					{
						if (!hits.Any((x) => x.Transform == hit.transform))
						{
							hits.Add(new HitScanHitData(hit, toNext.normalized));
						}
					}
				}
			}

			return hits;
		}

		/// <summary>
		/// Utilizes a collider for the scan shape.
		/// </summary>
		public static List<HitScanHitData> ColliderScan(
			Vector3 orbitPoint, Collider collider,
			(Vector3 pos, Quaternion rot) lastOrientation,
			int scans, LayerMask layerMask)
		{
			Vector3 scale = collider.transform.lossyScale;

			switch (collider)
			{
				case BoxCollider boxCollider:
					{
						Vector3 offset = boxCollider.center.Multiply(scale);
						var a = (lastOrientation.pos + lastOrientation.rot * offset, lastOrientation.rot);
						var b = (collider.transform.position + collider.transform.rotation * offset, collider.transform.rotation);
						return BoxScan(orbitPoint, boxCollider.size.Multiply(scale), a, b, scans, layerMask);
					}
				case SphereCollider sphereCollider:
					{
						Vector3 offset = sphereCollider.center.Multiply(scale);
						var a = (lastOrientation.pos + lastOrientation.rot * offset, lastOrientation.rot);
						var b = (collider.transform.position + collider.transform.rotation * offset, collider.transform.rotation);
						return SphereScan(orbitPoint, sphereCollider.radius * scale.magnitude, a, b, scans, layerMask);
					}
				default:
					SpaxDebug.Error($"Collider not supported: '{collider.GetType().Name}'.", "Please add support or change the collider type.");
					return null;
			}
		}

		public static List<HitScanHitData> SphereScan(
			Vector3 orbitPoint, float radius,
			(Vector3 pos, Quaternion rot) a, (Vector3 pos, Quaternion rot) b,
			int scans, LayerMask layerMask)
		{
			List<HitScanPoint> scanPoints = GetHitScanPoints(orbitPoint, a, b, scans);
			return SphereScan(scanPoints, radius, layerMask);
		}

		public static List<HitScanHitData> SphereScan(List<HitScanPoint> scanPoints, float radius, LayerMask layerMask)
		{
			return HitScan(scanPoints,
				(HitScanPoint current, HitScanPoint next, Vector3 toNext) =>
				{
					if (toNext == Vector3.zero)
					{
						return new RaycastHit[0];
					}

					RaycastHit[] hits = Physics.SphereCastAll(current.Center, radius, toNext.normalized, toNext.magnitude, layerMask);
#if UNITY_EDITOR
					if (DEBUG)
					{
						DbgDraw.WireSphere(current.Center, current.Rotation, new Vector3(radius, radius, radius), Color.cyan, DEBUG_DURATION);
						DbgDraw.WireTube(current.Center + toNext * 0.5f, Quaternion.LookRotation(Vector3.up.Look(toNext.normalized)), new Vector3(radius, toNext.magnitude, radius), Color.blue, DEBUG_DURATION);
					}
#endif
					return hits;
				}
			);
		}

		public static List<HitScanHitData> BoxScan(
			Vector3 orbitPoint, Vector3 boxSize,
			(Vector3 pos, Quaternion rot) a, (Vector3 pos, Quaternion rot) b,
			int scans, LayerMask layerMask)
		{
			List<HitScanPoint> scanPoints = GetHitScanPoints(orbitPoint, a, b, scans);
			return BoxScan(scanPoints, boxSize, layerMask);
		}

		public static List<HitScanHitData> BoxScan(List<HitScanPoint> boxScanPoints, Vector3 boxSize, LayerMask layerMask)
		{
			return HitScan(boxScanPoints,
				(HitScanPoint current, HitScanPoint next, Vector3 toNext) =>
				{
					if (toNext == Vector3.zero)
					{
						return new RaycastHit[0];
					}

					RaycastHit[] hits = Physics.BoxCastAll(current.Center, boxSize * 0.5f, toNext.normalized, current.Rotation, toNext.magnitude, layerMask);
#if UNITY_EDITOR
					if (DEBUG)
					{
						DbgDraw.WireCube(current.Center, current.Rotation, boxSize, Color.green, DEBUG_DURATION, false);
					}
#endif
					return hits;
				}
			);
		}

		#endregion // Hit Scans

		#region Scan Points

		public static List<HitScanPoint> GetHitScanPoints(
			Vector3 orbitPoint,
			(Vector3 pos, Quaternion rot) a, (Vector3 pos, Quaternion rot) b,
			int interpolations)
		{
			if (interpolations < 2)
			{
				SpaxDebug.Error("A minimum of 2 interpolations is required to perform a hit sweep.");
				return null;
			}

			interpolations -= 1;

			List<HitScanPoint> scanPoints = new List<HitScanPoint>();
			for (int i = 0; i <= interpolations; i++)
			{
				float interpolationValue = (float)i / interpolations;
				scanPoints.Add(GetHitScanPoint(orbitPoint, a, b, interpolationValue));
			}
			return scanPoints;
		}

		/// <summary>
		/// Calculate new <see cref="HitScanPoint"/> between <paramref name="a"/> and <paramref name="b"/> at <paramref name="interpolationValue"/>.
		/// </summary>
		/// <param name="orbitPoint">The point around which the scan orbits.</param>
		/// <param name="a">Orientation at the beginning of the scan.</param>
		/// <param name="b">Orientation at the end of the scan.</param>
		/// <param name="interpolationValue">How far along the scan between <paramref name="a"/> and <paramref name="b"/> should the point be calculated.</param>
		/// <returns>New <see cref="HitScanPoint"/> between <paramref name="a"/> and <paramref name="b"/> at <paramref name="interpolationValue"/>.</returns>
		public static HitScanPoint GetHitScanPoint(
			Vector3 orbitPoint,
			(Vector3 pos, Quaternion rot) a, (Vector3 pos, Quaternion rot) b,
			float interpolationValue)
		{
			// We calculate the position by Slerping two offsets with as base the center of the entity.
			// This way the calculated sweep will actually follow a curved trajectory instead of a straight path.
			// This is important when an attack takes place within 1 or 2 frames and not enough data is available to calculate an accurate sweep.

			HitScanPoint scanPoint = new HitScanPoint();
			Vector3 positionAOffset = a.pos - orbitPoint;
			Vector3 positionBOffset = b.pos - orbitPoint;
			Vector3 offset = Vector3.Slerp(positionAOffset, positionBOffset, interpolationValue);

			scanPoint.Position = orbitPoint + offset;
			scanPoint.Rotation = Quaternion.Lerp(a.rot, b.rot, interpolationValue);
			scanPoint.Center = scanPoint.Position;
			return scanPoint;
		}

		#endregion // Scan Points
	}
}
