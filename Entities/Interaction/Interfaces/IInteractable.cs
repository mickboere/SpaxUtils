using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects able to be interacted with.
	/// </summary>
	public interface IInteractable : IEntityComponent
	{
		/// <summary>
		/// The type of this interactable, used to identify which actions can be performed upon it.
		/// </summary>
		string InteractableType { get; }

		/// <summary>
		/// Returns whether this interactable can currently be interacted with.
		/// </summary>
		bool IsInteractable { get; }

		/// <summary>
		/// Returns a list of available interactions for this interactable.
		/// </summary>
		List<string> GetInteractions(IEntity interactor);

		/// <summary>
		/// Try to interact with this interactable using <paramref name="interaction"/>.
		/// </summary>
		bool TryInteract(IInteraction interaction);
	}
}
