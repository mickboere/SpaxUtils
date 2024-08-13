using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Global service that keeps track of all currently active <see cref="IProjectile"/>s.
	/// </summary>
	public class ProjectileService : IService
	{
		/// <summary>
		/// All of the recorded active projectiles.
		/// </summary>
		public IReadOnlyCollection<IProjectile> Projectiles => projectiles;

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
		/// Returns whether <paramref name="targetable"/> is currently being targeted by any projectiles.
		/// </summary>
		/// <param name="targetable">The <see cref="ITargetable"/> to check.</param>
		/// <param name="projectiles">The projectiles currently targetting <paramref name="targetable"/>, if any.</param>
		/// <returns>Whether <paramref name="targetable"/> is currently being targeted by any projectiles.</returns>
		public bool IsTarget(ITargetable targetable, out IProjectile[] projectiles)
		{
			projectiles = this.projectiles.Where((p) => p.Target == targetable).ToArray();
			return projectiles.Length > 0;
		}

		/// <summary>
		/// Returns whether <paramref name="point"/> is currently on a collision course with any projectile.
		/// </summary>
		/// <param name="point">The point to check projectile collisions for.</param>
		/// <param name="radius">The hit-radius of the point.</param>
		/// <param name="delta">The time delta to project.</param>
		/// <param name="projectile">The projectile set to hit <paramref name="point"/>, if any.</param>
		/// <returns>Whether <paramref name="point"/> is currently on a collision course with any projectile.</returns>
		public bool IsInPath(Vector3 point, float radius, float delta, out Vector3 closest, out float distance, out IProjectile projectile)
		{
			closest = Vector3.zero;
			distance = 0f;
			projectile = null;

			foreach (IProjectile p in projectiles)
			{
				if (p.IsInPath(point, radius, delta, out closest, out distance))
				{
					projectile = p;
					return true;
				}
			}

			return false;
		}
	}
}
