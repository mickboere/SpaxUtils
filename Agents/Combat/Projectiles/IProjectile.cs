using UnityEngine;

namespace SpaxUtils
{
	public interface IProjectile
	{
		Vector3 Point { get; }
		float Radius { get; }
		float Speed { get; }
		Vector3 Velocity { get; }

		IAgent Source { get; }
		ITargetable Target { get; }
		bool Hostile { get; }
		bool Friendly { get; }
	}
}
