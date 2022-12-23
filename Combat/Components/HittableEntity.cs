using System;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IHittable"/> implementation that passes along incoming hits.
	/// </summary>
	public class HittableEntity : EntityComponentBase, IHittable
	{
		/// <inheritdoc/>
		public event Action<HitData> OnHitEvent;

		/// <inheritdoc/>
		public void Hit(HitData hitData)
		{
			OnHitEvent?.Invoke(hitData);
		}
	}
}
