using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Utility functions for combat.
	/// </summary>
	public static class CombatUtils
	{
		public struct SweepPointMatrix
		{
			public Vector3 Position { get; set; }
			public Quaternion Rotation { get; set; }
			public Vector3 Center { get; set; }
			public Vector3 Forward => Rotation * Vector3.forward;
			public Vector3 Up => Rotation * Vector3.up;
		}

		/// <summary>
		/// Performs a sweep of box casts between two points.
		/// </summary>
		/// <param name="entityCenter">The orbit point of the weapon.</param>
		/// <param name="weaponSize">The size of the weapon. X = width, Y = thickness, Z = length.</param>
		/// <param name="positionA">Starting position of the sweep.</param>
		/// <param name="rotationA">Starting rotation of the sweep.</param>
		/// <param name="positionB">End position of the sweep.</param>
		/// <param name="rotationB">End rotation of the sweep.</param>
		/// <param name="scans">Amount of scans to perform between the start and end point.</param>
		/// <param name="layerMask">LayerMask used by the box casts.</param>
		/// <returns>A list of <see cref="HitScanHitData"/> containing all the objects that were hit during the sweep with additional data.</returns>
		public static List<HitScanHitData> PerformHitSweep(
			Vector3 entityCenter,
			Vector3 weaponSize,
			Vector3 positionA, Quaternion rotationA,
			Vector3 positionB, Quaternion rotationB,
			int scans,
			LayerMask layerMask)
		{
			List<HitScanHitData> sweepHits = new List<HitScanHitData>();
			List<SweepPointMatrix> sweepPoints = GetSweepPointMatrixes(entityCenter, weaponSize, positionA, rotationA, positionB, rotationB, scans);
			for (int i = 0; i < sweepPoints.Count - 1; i++)
			{
				SweepPointMatrix current = sweepPoints[i];
				SweepPointMatrix next = sweepPoints[i + 1];

				Vector3 toNextScan = next.Center - current.Center;
				RaycastHit[] scanHits = Physics.BoxCastAll(current.Center, weaponSize * 0.5f, toNextScan.normalized, current.Rotation, toNextScan.magnitude, layerMask);
				foreach (RaycastHit hit in scanHits)
				{
					if (!sweepHits.Any((x) => x.Transform == hit.transform))
					{
						sweepHits.Add(new HitScanHitData(hit, toNextScan.normalized));
					}
				}
			}

			return sweepHits;
		}

		/// <summary>
		/// Returns a list of pre-calculated sweep points matrixes along a path.
		/// </summary>
		/// <param name="entityCenter">The orbit point of the weapon.</param>
		/// <param name="weaponSize">The size of the weapon. X = width, Y = thickness, Z = length.</param>
		/// <param name="positionA">Starting position of the sweep.</param>
		/// <param name="rotationA">Starting rotation of the sweep.</param>
		/// <param name="positionB">End position of the sweep.</param>
		/// <param name="rotationB">End rotation of the sweep.</param>
		/// <param name="interpolations">The amount of interpolations to perform between A and B.</param>
		/// <returns>Collection of <see cref="SweepPointMatrix"/>es that make up the sweep.</returns>
		public static List<SweepPointMatrix> GetSweepPointMatrixes(
			Vector3 entityCenter,
			Vector3 weaponSize,
			Vector3 positionA, Quaternion rotationA,
			Vector3 positionB, Quaternion rotationB,
			int interpolations)
		{
			if (interpolations < 2)
			{
				Debug.LogError("A minimum of 2 interpolations is required to perform a hit sweep.");
				return null;
			}

			interpolations -= 1;

			List<SweepPointMatrix> sweepPoints = new List<SweepPointMatrix>();
			for (int i = 0; i <= interpolations; i++)
			{
				float interpolationValue = (float)i / interpolations;
				sweepPoints.Add(GetSweepPointMatrix(entityCenter, weaponSize, positionA, rotationA, positionB, rotationB, interpolationValue));
			}
			return sweepPoints;
		}

		/// <summary>
		/// Returns point data along a sweep.
		/// </summary>
		/// <param name="entityCenter">The orbit point of the weapon.</param>
		/// <param name="weaponSize">The size of the weapon. X = width, Y = thickness, Z = length.</param>
		/// <param name="positionA">Starting position of the sweep.</param>
		/// <param name="rotationA">Starting rotation of the sweep.</param>
		/// <param name="positionB">End position of the sweep.</param>
		/// <param name="rotationB">End rotation of the sweep.</param>
		/// <param name="interpolationValue"></param>
		/// <returns>Point data along a sweep.</returns>
		public static SweepPointMatrix GetSweepPointMatrix(
			Vector3 entityCenter,
			Vector3 weaponSize,
			Vector3 positionA, Quaternion rotationA,
			Vector3 positionB, Quaternion rotationB,
			float interpolationValue)
		{
			// We calculate the position by Slerping two offsets with as base the center of the entity.
			// This way the calculated sweep will actually follow a curved trajectory instead of a straight path.
			// This is important when we add an attack where the sweep takes place in 1 or 2 frames and not enough data is available to calculate an accurate sweep.
			SweepPointMatrix matrix = new SweepPointMatrix();
			Vector3 positionAOffset = positionA - entityCenter;
			Vector3 positionBOffset = positionB - entityCenter;
			Vector3 offset = Vector3.Slerp(positionAOffset, positionBOffset, interpolationValue);
			matrix.Position = entityCenter + offset;
			matrix.Rotation = Quaternion.Lerp(rotationA, rotationB, interpolationValue);
			matrix.Center = matrix.Position + matrix.Forward * weaponSize.z * 0.5f;
			return matrix;
		}
	}
}