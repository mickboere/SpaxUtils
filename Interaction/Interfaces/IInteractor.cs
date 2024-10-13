using System;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects able to initiate <see cref="IInteraction"/>s with <see cref="IInteractable"/>s.
	/// </summary>
	public interface IInteractor : IEntityComponent
	{
		/// <summary>
		/// Invoked when this interactor enters an interaction.
		/// </summary>
		event Action<IInteraction> InteractorEvent;

		/// <summary>
		/// The interactor's interaction point in world-space.
		/// </summary>
		Vector3 InteractorPoint { get; }

		/// <summary>
		/// The interaction range from the <see cref="InteractorPoint"/>.
		/// </summary>
		float InteractorRange { get; }

		/// <summary>
		/// Returns whether this handler is able to set up a new interaction of type <paramref name="interactionType"/>.
		/// </summary>
		bool CanInteract(string interactionType);

		/// <summary>
		/// Will try to set up a new interaction of type <paramref name="interactionType"/>.
		/// </summary>
		/// <param name="interactable">The <see cref="IInteractable"/> this interactor should attempt interaction with.</param>
		/// <param name="interactionType">The type of interaction to try and set up.</param>
		/// <param name="interaction">The resulting <see cref="IInteraction"/> object.</param>
		/// <param name="data">The shared <see cref="IInteraction.Data"/>.</param>
		/// <returns>Whether setting up the interaction was a success.</returns>
		bool TryCreateInteraction(string interactionType, IInteractable interactable, out IInteraction interaction, object data = null);

		/// <summary>
		/// Returns all possible interactions between this <see cref="IInteractor"/> and the <paramref name="interactable"/>.
		/// </summary>
		/// <param name="interactable">The <see cref="IInteractable"/> to get all possible interactions for.</param>
		/// <returns>All possible interactions between this <see cref="IInteractor"/> and the <paramref name="interactable"/>.</returns>
		string[] GetAttemptableInteractions(IInteractable interactable) => interactable.InteractableTypes.Where(i => CanInteract(i)).ToArray();
	}
}
