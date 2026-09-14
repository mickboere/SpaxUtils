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
		/// Subscribes to all successful hit events. Sorted by <paramref name="order"/> like DefaultExecutionOrder; lowest goes first.
		/// </summary>
		/// <param name="listener">The listener object to subscribe.</param>
		/// <param name="callback">The callback to invoke once the hittable is hit.</param>
		/// <param name="order">Execution order of the listener, lowest is invoked first.</param>
		void Subscribe(object listener, Action<HitData> callback, int order = 0);

		/// <summary>
		/// Unsubscribes from hit events.
		/// </summary>
		/// <param name="listener">The listener object to unsubscribe.</param>
		void Unsubscribe(object listener);
	}
}
