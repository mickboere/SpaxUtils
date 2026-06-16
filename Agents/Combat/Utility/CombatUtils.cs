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
					Debug.DrawLine(current.Center, next.Center, color, DEBUG_DURATION);
				}
#endif

				if (scanHits != null)
				{
					foreach (RaycastHit hit in scanHits)
					{
						if (!hits.Any((x) => x.Transform == hit.transform))
						{
							hits.Add(new HitScanHitData(hit, toNext.normalized, current.Center));
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
			Vector3 absScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

			switch (collider)
			{
				case BoxCollider boxCollider:
					{
						Vector3 offset = boxCollider.center.Multiply(scale);
						var a = (lastOrientation.pos + lastOrientation.rot * offset, lastOrientation.rot);
						var b = (collider.transform.position + collider.transform.rotation * offset, collider.transform.rotation);
						// Box size scales per-axis with lossy scale (not by its magnitude).
						Vector3 boxSize = boxCollider.size.Multiply(absScale);
						int boxScans = ComputeScanCount(a, b, Mathf.Min(boxSize.x, Mathf.Min(boxSize.y, boxSize.z)) * 0.5f, boxSize.magnitude * 0.5f, scans);
						return BoxScan(orbitPoint, boxSize, a, b, boxScans, layerMask);
					}
				case SphereCollider sphereCollider:
					{
						Vector3 offset = sphereCollider.center.Multiply(scale);
						var a = (lastOrientation.pos + lastOrientation.rot * offset, lastOrientation.rot);
						var b = (collider.transform.position + collider.transform.rotation * offset, collider.transform.rotation);
						// Sphere radius scales with the largest axis (matches Unity).
						float sphereRadius = sphereCollider.radius * Mathf.Max(absScale.x, Mathf.Max(absScale.y, absScale.z));
						// Rotation doesn't change a sphere's swept volume, so extent is 0 - translation only.
						int sphereScans = ComputeScanCount(a, b, sphereRadius, 0f, scans);
						return SphereScan(orbitPoint, sphereRadius, a, b, sphereScans, layerMask);
					}
				case CapsuleCollider capsuleCollider:
					{
						Vector3 offset = capsuleCollider.center.Multiply(scale);
						var a = (lastOrientation.pos + lastOrientation.rot * offset, lastOrientation.rot);
						var b = (collider.transform.position + collider.transform.rotation * offset, collider.transform.rotation);
						// Radius scales with the larger of the two axes PERPENDICULAR to the capsule direction;
						// height scales with the axis ALONG the direction (matches Unity).
						Vector3 axis;
						float radiusScale;
						float heightScale;
						switch (capsuleCollider.direction)
						{
							case 2: axis = Vector3.forward; radiusScale = Mathf.Max(absScale.x, absScale.y); heightScale = absScale.z; break;
							case 1: axis = Vector3.up; radiusScale = Mathf.Max(absScale.x, absScale.z); heightScale = absScale.y; break;
							default: axis = Vector3.right; radiusScale = Mathf.Max(absScale.y, absScale.z); heightScale = absScale.x; break;
						}
						float capsuleRadius = capsuleCollider.radius * radiusScale;
						// Unity clamps a capsule's height to at least its diameter.
						float capsuleHeight = Mathf.Max(capsuleCollider.height * heightScale, capsuleRadius * 2f);
						int capsuleScans = ComputeScanCount(a, b, capsuleRadius, capsuleHeight * 0.5f, scans);
						return CapsuleScan(orbitPoint, capsuleRadius, capsuleHeight, axis, a, b, capsuleScans, layerMask);
					}
				default:
					SpaxDebug.Error($"Collider not supported:", $"'{collider.GetType().Name}' Please add support or change the collider type.");
					return null;
			}
		}

		#region Box

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
		#endregion Box

		#region Sphere

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
						DbgDraw.WireTube(current.Center + toNext * 0.5f, Quaternion.LookRotation(Vector3.up.Look(toNext.normalized)), new Vector3(radius, toNext.magnitude, radius), Color.cyan, DEBUG_DURATION);
					}
#endif
					return hits;
				}
			);
		}

		#endregion Sphere

		#region Capsule

		public static List<HitScanHitData> CapsuleScan(
			Vector3 orbitPoint, float radius, float height, Vector3 axis,
			(Vector3 pos, Quaternion rot) a, (Vector3 pos, Quaternion rot) b,
			int scans, LayerMask layerMask)
		{
			List<HitScanPoint> scanPoints = GetHitScanPoints(orbitPoint, a, b, scans);
			return CapsuleScan(scanPoints, radius, height, axis, layerMask);
		}

		public static List<HitScanHitData> CapsuleScan(List<HitScanPoint> scanPoints, float radius, float height, Vector3 axis, LayerMask layerMask)
		{
			return HitScan(scanPoints,
				(HitScanPoint current, HitScanPoint next, Vector3 toNext) =>
				{
					if (toNext == Vector3.zero)
					{
						return new RaycastHit[0];
					}

					// Sphere-centre offset from the capsule centre = half the cylinder length (half-height minus the
					// radius), matching how Unity places a CapsuleCollider's end spheres. CapsuleCastAll takes the
					// two sphere CENTRES, so this must not include the radius.
					Vector3 direction = current.Rotation * axis * Mathf.Max(0f, height * 0.5f - radius);
					RaycastHit[] hits = Physics.CapsuleCastAll(current.Center - direction, current.Center + direction, radius, toNext.normalized, toNext.magnitude, layerMask);
#if UNITY_EDITOR
					if (DEBUG)
					{
						// WireCapsule draws along its local up, so rotate local up onto the collider's capsule
						// axis: rotation * FromToRotation(up, axis). radius/height are the real world dimensions,
						// so the gizmo traces the true cast volume. One per scan point (like the box) - the next
						// segment draws its own start, so drawing the end too would double up interior points.
						DbgDraw.WireCapsule(current.Center, current.Rotation * Quaternion.FromToRotation(Vector3.up, axis), radius, height, Color.blue, DEBUG_DURATION);
					}
#endif
					return hits;
				}
			);
		}

		#endregion Capsule

		#endregion Hit Scans

		#region Scan Points

		/// <summary>
		/// Adaptive sub-scan count for one frame's sweep: just enough linear sub-casts that the curved
		/// (orbited + rotated) path between the two orientations has no gap a target could slip through - and no
		/// more. Returns 2 (a single cast) when the shape barely moved, scaling up only as it moves faster.
		/// </summary>
		/// <param name="shapeRadius">Shape thickness, used as the per-segment overlap budget.</param>
		/// <param name="shapeExtent">Distance from the shape centre to its furthest point, for the rotation arc.</param>
		/// <param name="maxScans">Upper safety clamp.</param>
		public static int ComputeScanCount(
			(Vector3 pos, Quaternion rot) a, (Vector3 pos, Quaternion rot) b,
			float shapeRadius, float shapeExtent, int maxScans)
		{
			// Worst-case travel of the shape's furthest point = centre translation + the arc it sweeps as the
			// shape rotates. Each sub-cast already sweeps continuously along its own chord, so we only need
			// enough chords that none exceeds ~the shape's thickness.
			float translation = Vector3.Distance(a.pos, b.pos);
			float rotationArc = shapeExtent * Quaternion.Angle(a.rot, b.rot) * Mathf.Deg2Rad;
			float step = Mathf.Max(shapeRadius, 0.001f);

			int segments = Mathf.CeilToInt((translation + rotationArc) / step);
			return Mathf.Clamp(segments + 1, 2, maxScans);
		}

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

		#region Charge Prediction

		/// <summary>
		/// Predict how much we intend to charge and how long that will take.
		/// intent: 0..1 (0 = minimum charge only, 1 = as close to MaxCharge as possible).
		/// </summary>
		public static void GetChargePrediction(
			ICombatMove move,
			float intent,
			float chargeSpeed,
			out float chargeTime,
			out float chargeFactor)
		{
			if (move == null || !move.HasCharge)
			{
				chargeTime = 0f;
				chargeFactor = 1f; // no extra damage / storm
				return;
			}

			float invSpeed = 1f / Mathf.Max(chargeSpeed, 0.01f);
			float minT = move.MinCharge * invSpeed;
			float maxT = move.MaxCharge > 0f ? move.MaxCharge * invSpeed : minT;

			intent = Mathf.Clamp01(intent);
			float t = Mathf.Lerp(minT, maxT, intent);

			chargeTime = t;

			// Map charge time into an "excess" factor 1..2:
			//  1   = minimum charge,
			//  2   = maximum charge.
			if (maxT > minT)
			{
				float norm = Mathf.InverseLerp(minT, maxT, t); // 0..1
				chargeFactor = 1f + norm; // 1..2
			}
			else
			{
				chargeFactor = 1f;
			}
		}

		/// <summary>
		/// Given a melee move and a chargeFactor (1..2), estimate the storm distance
		/// and effective reach (baseReach + storm travel).
		/// </summary>
		public static float GetStormReach(IMeleeCombatMove meleeMove, float baseReach, float chargeFactor, out float stormDistance)
		{
			stormDistance = 0f;
			if (meleeMove == null || meleeMove.StormDistance <= 0f || chargeFactor <= 1f)
			{
				return baseReach;
			}

			// Only the "excess" over 1 increases storm distance.
			float excess = Mathf.Clamp01(chargeFactor - 1f); // 0..1
			stormDistance = meleeMove.StormDistance * excess;

			return baseReach + stormDistance;
		}

		#endregion // Charge Prediction

		#region Ally Threat

		/// <summary>
		/// Finds the primary threat directed at <paramref name="allyInfo"/>'s agent.
		/// Priority 1: <see cref="AgentCombatComponent.LastAttacker"/> (most recent hit).
		/// Priority 2: highest-Threat enemy currently targeting the ally via <see cref="TargetingService"/>.
		/// </summary>
		public static IAgent FindPrimaryThreat(
			AllyInfo allyInfo,
			TargetingService targetingService,
			CombatSensesComponent combatSenses,
			IAgent self)
		{
			if (allyInfo == null)
				return null;

			// P1: last attacker.
			IEntity lastAttacker = allyInfo.CombatComp?.LastAttacker;
			if (lastAttacker is IAgent attackerAgent && attackerAgent.Alive)
				return attackerAgent;

			// P2: scan targeters via service.
			ITargetable allyTargetable = allyInfo.Agent?.Targetable;
			if (allyTargetable == null || targetingService == null)
				return null;

			IAgent bestThreat = null;
			float bestScore = -1f;

			foreach (ITargeter targeter in targetingService.GetTargeters(allyTargetable))
			{
				if (targeter == self?.Targeter)
					continue;

				IAgent threatening = targeter.TargetAgent;
				if (threatening == null || !threatening.Alive)
					continue;

				float score = 0.5f;
				ITargetable tgt = threatening.Targetable;
				if (tgt != null && combatSenses?.EnemySense != null)
				{
					EnemyInfo info = combatSenses.EnemySense.GetEnemyInfo(tgt);
					if (info != null)
						score = info.Threat;
				}

				if (score > bestScore)
				{
					bestScore = score;
					bestThreat = threatening;
				}
			}

			return bestThreat;
		}

		#endregion // Ally Threat
	}
}
