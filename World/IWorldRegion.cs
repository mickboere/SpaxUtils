using UnityEngine;

namespace SpaxUtils
{
	public interface IWorldRegion
	{
		/// <summary>
		/// The priority of this region, higher prio takes priority.
		/// </summary>
		int Prio { get; }

		/// <summary>
		/// Whether this region has a specific entry zone.
		/// </summary>
		bool HasEntry { get; }

		/// <summary>
		/// Whether this region has a specific exit zone.
		/// </summary>
		bool HasExit { get; }

		/// <summary>
		/// Returns whether <paramref name="point"/> lies within the region.
		/// </summary>
		/// <param name="point">The world space position to check.</param>
		bool IsInside(Vector3 point);

		/// <summary>
		/// Returns whether <paramref name="point"/> lies within an entry-zone.
		/// </summary>
		/// <param name="point">The world space position to check.</param>
		bool HasEntered(Vector3 point);

		/// <summary>
		/// Returns whether <paramref name="point"/> lies outside of the exit-zones.
		/// </summary>
		/// <param name="point">The world space position to check.</param>
		bool HasExited(Vector3 point);
	}
}
