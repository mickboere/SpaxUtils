using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects able to create <see cref="IInteraction"/>s for <see cref="IInteractable"/>s of any type.
	/// </summary>
	public interface IInteractor : IEntityComponent
	{
		/// <summary>
		/// Returns a list of available interactions for <paramref name="interactable"/>.
		/// </summary>
		List<string> GetInteractions(IInteractable interactable);

		/// <summary>
		/// Try to create a new interaction which aims to perform <paramref name="action"/> upon <paramref name="interactable"/>.
		/// </summary>
		/// <param name="interactable">The interactor to interact with.</param>
		/// <param name="action">The action to perform upon the interactable.</param>
		/// <param name="interaction">If successful, the resulting interaction object.</param>
		/// <returns>Whether the interaction was succesfully initiated.</returns>
		bool TryCreateInteraction(IInteractable interactable, string action, out IInteraction interaction);
	}
}
