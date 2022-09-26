using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> that handles all interactions for an entity, implements both <see cref="IInteractor"/> and <see cref="IInteractable"/>.
	/// </summary>
	public interface IInteractionHandler : IInteractor, IInteractable
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
		/// Adds an <see cref="IInteractor"/> to this component.
		/// </summary>
		void AddInteractor(IInteractor interactor);

		/// <summary>
		/// Removes an <see cref="IInteractor"/> from this component.
		/// </summary>
		void RemoveInteractor(IInteractor interactor);

		/// <summary>
		/// Adds an <see cref="IInteractable"/> to this component.
		/// </summary>
		void AddInteractable(IInteractable interactable);

		/// <summary>
		/// Removes an <see cref="IInteractable"/> from this component.
		/// </summary>
		void RemoveInteractable(IInteractable interactable);

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

		/// <summary>
		/// Returns all the <see cref="IInteractionBlocker"/>s that are blocking interactions of type <paramref name="interactionType"/>.
		/// </summary>
		IList<IInteractionBlocker> GetBlockers(string interactionType);

		/// <summary>
		/// Returns whether the <paramref name="interactionType"/> is blocked from being handled.
		/// </summary>
		bool IsBlocked(string interactionType);
	}
}
