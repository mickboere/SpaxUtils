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
		/// Returns all unoccupied points of interest belonging to this region that match the required labels.
		/// </summary>
		List<PointOfInterest> GetAvailablePOIs(string[] requiredLabels = null);
	}
}
