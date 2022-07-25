using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> that is able to both give consent to an incoming interaction type, or attempt to perform an <see cref="IInteraction"/>.
	/// </summary>
	/// <seealso cref="IEntityComponent"/>
	/// <seealso cref="IInteractionHandler"/>
	public interface IInteractingComponent : IEntityComponent, IInteractionHandler
	{
		/// <summary>
		/// The interaction point in world-space.
		/// </summary>
		Vector3 InteractionPoint { get; }

		/// <summary>
		/// The interaction range from the interaction point.
		/// </summary>
		float InteractionRange { get; }

		/// <summary>
		/// Returns all the <see cref="IInteractionBlocker"/>s that are blocking interactions of type <paramref name="interactionType"/>.
		/// </summary>
		/// <param name="interactionType"></param>
		/// <returns></returns>
		IList<IInteractionBlocker> GetBlockers(string interactionType);

		/// <summary>
		/// Adds an <see cref="IInteractionHandler"/> implementation to this component.
		/// <see cref="IInteractionHandler"/>s are able to handle incoming interaction requests of specific types.
		/// </summary>
		void AddHandler(IInteractionHandler interactionHandler);

		/// <summary>
		/// Removes an <see cref="IInteractionHandler"/> implementation from this component.
		/// <see cref="IInteractionHandler"/>s are able to handle incoming interaction requests of specific types.
		/// </summary>
		void RemoveHandler(IInteractionHandler interactionHandler);

		/// <summary>
		/// Adds an <see cref="IInteractionBlocker"/> implementation to this component.
		/// <see cref="IInteractionBlocker"/>s are able to block incoming interaction requests of specific types.
		/// </summary>
		void AddBlocker(IInteractionBlocker interactionBlocker);

		/// <summary>
		/// Removes an <see cref="IInteractionBlocker"/> implementation from this component.
		/// <see cref="IInteractionBlocker"/>s are able to block incoming interaction requests of specific types.
		/// </summary>
		void RemoveBlocker(IInteractionBlocker interactionBlocker);
	}
}
