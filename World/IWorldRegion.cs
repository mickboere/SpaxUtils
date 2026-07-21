using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public interface IWorldRegion
	{
		/// <summary>
		/// The priority of this region. Higher priority takes precedence when regions overlap.
		/// </summary>
		int Prio { get; }

		/// <summary>
		/// Returns whether <paramref name="point"/> lies within the region.
		/// </summary>
		bool IsInside(Vector3 point);

		/// <summary>
		/// Returns a random point sampled uniformly within the region's volume.
		/// Larger sub-regions are sampled proportionally more often.
		/// </summary>
		Vector3 SamplePoint();

		/// <summary>
		/// Returns the closest point that lies within (or on the surface of) the region to <paramref name="point"/>.
		/// If <paramref name="point"/> is already inside, it is returned unchanged.
		/// </summary>
		Vector3 GetClosestPointWithinRegion(Vector3 point);

		/// <summary>
		/// Horizontal distance from <paramref name="point"/> to the nearest TRUE outer border, plus the inward normal
		/// there. Y is ignored for direction (a floor/ceiling is not a border), though a sphere's slice radius still
		/// varies with height. Boundary points falling inside another sub-region are internal seams and are skipped,
		/// so a composite region reads as one arena. Returns false when there is no border nearby to steer away from
		/// (outside the horizontal footprint, or every candidate was a seam).
		/// <paramref name="maxDepth"/> is the deepest <paramref name="depth"/> obtainable in the same sub-region (its
		/// horizontal inradius), so callers can shrink an absolute margin to fit a region too narrow to hold it.
		/// </summary>
		bool TryGetBorderDepth(Vector3 point, out float depth, out Vector3 inwardNormal, out float maxDepth);

		/// <summary>
		/// Returns all unoccupied points of interest belonging to this region that match the required labels.
		/// </summary>
		List<PointOfInterest> GetAvailablePOIs(string[] requiredLabels = null);
	}
}
