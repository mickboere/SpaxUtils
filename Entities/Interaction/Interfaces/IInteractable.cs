using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects able to be interacted with by <see cref="IInteractor"/>s.
	/// </summary>
	public interface IInteractable : IEntityComponent
	{
		/// <summary>
		/// Invoked when this interactable enters an interaction.
		/// </summary>
		event Action<IInteraction> InteractableEvent;

		/// <summary>
		/// The interactor's interaction point in world-space.
		/// </summary>
		Vector3 InteractablePoint { get; }

		/// <summary>
		/// The interaction range from the <see cref="InteractablePoint"/>.
		/// </summary>
		float InteractableRange { get; }

		/// <summary>
		/// Gets all interaction types this interactable supports.
		/// </summary>
		string[] InteractableTypes { get; }

		/// <summary>
		/// Returns whether this interactable supports interactions of type <paramref name="interactionType"/>.
		/// </summary>
		/// <param name="interactionType"></param>
		/// <returns></returns>
		bool IsInteractable(string interactionType);

		/// <summary>
		/// Try to interact with this object using <paramref name="interaction"/>.
		/// </summary>
		/// <param name="interaction">The interaction to attempt.</param>
		/// <returns>TRUE when succeeded, FALSE when failed.</returns>
		bool TryInteract(IInteraction interaction);
	}
}
