using System;

namespace SpaxUtils
{
	/// <summary>
	/// Base interface for all objects that should be able to get "hit", as in a combat sense.
	/// </summary>
	public interface IHittable : IEntityComponent
	{
		/// <summary>
		/// Invoked before any processing of <see cref="HitData"/> is done.
		/// </summary>
		event Action<HitData> OnHitEvent;

		/// <summary>
		/// Invoked after the processing of the <see cref="HitData"/> is done.
		/// </summary>
		event Action<HitData> PostHitEvent;

		/// <summary>
		/// Mark this object as hit.
		/// </summary>
		/// <param name="hitData">Collection of data that can be filtered to retrieve whatever is relevant to the object being hit.</param>
		void Hit(HitData hitData);
	}
}
