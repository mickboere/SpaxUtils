using System;
using UnityEngine;

namespace SpaxUtils
{
	public class ProjectileSense : IDisposable
	{
		private IAgent agent;
		private ProjectileService projectileService;

		public ProjectileSense(IAgent agent, ProjectileService projectileService)
		{
			this.agent = agent;
			this.projectileService = projectileService;
		}

		public void Dispose()
		{

		}

		public void Sense(float delta)
		{
			// Projectile stimuli.
			// TODO: Split up projectile detection sense to a separte component which can be accesses by the behaviours to also track projectiles.
			if (projectileService.IsTarget(agent.Targetable, out IProjectile[] projectiles))
			{
				foreach (IProjectile projectile in projectiles)
				{
					if (agent.Targetable.Center.Distance(projectile.StartPoint) < projectile.Range)
					{
						Vector3 direction = agent.Targetable.Center - projectile.Point;
						float imminence = projectile.Speed / (direction.magnitude - (projectile.Radius + agent.Targetable.Size.x)).Max(1f); // The more imminent the projectile, the more dangerous.
						float danger = Mathf.Clamp(imminence * direction.ClampedDot(projectile.Velocity), 0f, AEMOI.MAX_STIM); // Dot: if projectile isn't pointing towards agent its not dangerous.
						// Continuous Demand LEVEL (danger already 0..MAX); the tracker provides the fall, so the old current-based relax is gone.
						Vector8 stim = new Vector8()
						{
							E = danger,
							W = danger
						};
						agent.Mind.SetDemand(stim, projectile.Source);
					}
				}
			}
		}
	}
}
