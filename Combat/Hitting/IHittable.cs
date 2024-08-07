using System;

namespace SpaxUtils
{
	/// <summary>
	/// Component interface for entities that should be able to get hit.
	/// </summary>
	public interface IHittable : IEntityComponent
	{
		/// <summary>
		/// Returns whether this entity is able to be hit, or if its invinvible.
		/// </summary>
		bool IsHittable { get; set; }

		/// <summary>
		/// Attempt to hit this hittable entity.
		/// </summary>
		/// <param name="hitData">The <see cref="HitData"/> to transfer to this entity.</param>
		/// <returns>Whether hitting this entity was succesful.</returns>
		bool Hit(HitData hitData);

		/// <summary>
		/// Subscribes to all successful hit events. Invocation order is sorted by <paramref name="prio"/>, highest prio is invoked first.
		/// </summary>
		/// <param name="listener">The listener object to subscribe.</param>
		/// <param name="callback">The callback to invoke once the hittable is hit.</param>
		/// <param name="prio">The priority of the listener, highest prio is invoked first.</param>
		void Subscribe(object listener, Action<HitData> callback, int prio = 0);

		/// <summary>
		/// Unsubscribes from hit events.
		/// </summary>
		/// <param name="listener">The listener object to unsubscribe.</param>
		void Unsubscribe(object listener);
	}
}
