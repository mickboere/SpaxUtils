using System;

namespace SpaxUtils
{
	/// <summary>
	/// Component interface for entities that should be able to get hit.
	/// </summary>
	public interface IHittable : IEntityComponent
	{
		/// <summary>
		/// Invoked upon being hit succesfully.
		/// </summary>
		event Action<HitData> OnHitEvent;

		/// <summary>
		/// Returns whether this entity is able to be hit, or if its invinvible.
		/// </summary>
		bool Hittable { get; }

		/// <summary>
		/// Attempt to hit this hittable entity.
		/// </summary>
		/// <param name="hitData">The <see cref="HitData"/> to transfer to this entity.</param>
		/// <returns>Whether hitting this entity was succesful.</returns>
		bool Hit(HitData hitData);
	}
}
