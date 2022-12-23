using System;

namespace SpaxUtils
{
	/// <summary>
	/// Component interface for entities that should be able to get hit.
	/// </summary>
	public interface IHittable : IEntityComponent
	{
		event Action<HitData> OnHitEvent;

		void Hit(HitData hitData);
	}
}
