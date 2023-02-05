using System;
using UnityEngine;

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
		public bool Hittable => hittable;

		[SerializeField] private bool hittable = true;

		/// <inheritdoc/>
		public bool Hit(HitData hitData)
		{
			if (Hittable)
			{
				OnHitEvent?.Invoke(hitData);
			}

			return Hittable;
		}
	}
}
