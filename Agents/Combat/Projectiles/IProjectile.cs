using UnityEngine;

namespace SpaxUtils
{
	public interface IProjectile
	{
		Vector3 Point { get; }
		float Radius { get; }
		Vector3 StartPoint { get; }
		float Range { get; set; }
		float Speed { get; }
		Vector3 Velocity { get; }

		IAgent Source { get; }
		ITargetable Target { get; }

		/// <summary>
		/// Returns whether <paramref name="point"/> is currently on a collision course with this projectile.
		/// </summary>
		/// <param name="point">The point to check collision for.</param>
		/// <param name="radius">The hit-radius of the point.</param>
		/// <param name="delta">The time delta to calculate the trajectory for.</param>
		/// <param name="closest">The closest point along the projected trajectory to <paramref name="point"/>.</param>
		/// <param name="distance">How far removed <paramref name="point"/> is from being hit.</param>
		/// <returns>Whether <paramref name="point"/> is currently on a collision course with this projectile.</returns>
		bool IsInPath(Vector3 point, float radius, float delta, out Vector3 closest, out float distance);
	}
}
