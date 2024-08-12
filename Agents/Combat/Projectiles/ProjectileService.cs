using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class ProjectileService : IService
	{
		private HashSet<IProjectile> projectiles = new HashSet<IProjectile>();

		public void Add(IProjectile projectile)
		{
			projectiles.Add(projectile);
		}

		public void Remove(IProjectile projectile)
		{
			if (projectiles.Contains(projectile))
			{
				projectiles.Remove(projectile);
			}
		}

		/// <summary>
		/// Returns whether <paramref name="point"/> is currently on a collision course with any projectile.
		/// </summary>
		/// <param name="point">The point to check projectile collisions for.</param>
		/// <param name="radius">The hit-radius of the point.</param>
		/// <param name="projectile">The projectile set to hit <paramref name="point"/>, if any.</param>
		/// <returns>Whether <paramref name="point"/> is currently on a collision course with any projectile.</returns>
		public bool IsInPath(Vector3 point, float radius, out IProjectile projectile)
		{
			foreach (IProjectile p in projectiles)
			{
				Vector3 a = p.Point;
				Vector3 b = a + p.Velocity * Time.deltaTime;
				Vector3 closest = point.ClosestOnLine(a, b);
				if (point.Distance(closest) < p.Radius + radius)
				{
					projectile = p;
					return true;
				}
			}

			projectile = null;
			return false;
		}
	}
}
