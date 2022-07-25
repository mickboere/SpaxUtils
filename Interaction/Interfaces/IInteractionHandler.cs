using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects that can handle any specific type of interaction(s).
	/// </summary>
	public interface IInteractionHandler
	{
		/// <summary>
		/// Invoked whenever a new <see cref="IInteraction"/> has been entered.
		/// </summary>
		event Action<IInteraction> EnteredInteractionEvent;

		/// <summary>
		/// All of the interaction types this handler is interactable with.
		/// </summary>
		List<string> InteractableTypes { get; }

		/// <summary>
		/// All non-concluded <see cref="IInteraction"/>s this class is currently handling.
		/// </summary>
		List<IInteraction> ActiveInteractions { get; }

		/// <summary>
		/// Ask whether this <see cref="IInteractionHandler"/> is interested in receiving an interaction of type <paramref name="interactionType"/>.
		/// </summary>
		/// <param name="interactor">The <see cref="IInteractingComponent"/> that wants to interact with this interactable.</param>
		/// <param name="interactionType">The type of interaction to propose.</param>
		/// <returns>Whether this <see cref="IInteractionHandler"/> is interested in receiving an interaction of type <paramref name="interactionType"/>.</returns>
		bool Interactable(IInteractingComponent interactor, string interactionType);

		/// <summary>
		/// Try to interact with this object using <paramref name="interaction"/>.
		/// </summary>
		/// <param name="interaction">The interaction to attempt.</param>
		/// <returns>TRUE when succeeded, FALSE when failed.</returns>
		bool Interact(IInteraction interaction);

		/// <summary>
		/// Whether this handler is able to set up a new interaction of type <paramref name="interactionType"/>.
		/// </summary>
		/// <param name="interactionType"></param>
		/// <returns></returns>
		bool Able(string interactionType);

		/// <summary>
		/// Will try to set up a new interaction of type <paramref name="interactionType"/>.
		/// </summary>
		/// <param name="interactionType">The type of interaction to try and set up.</param>
		/// <param name="data">The <see cref="IInteraction.Data"/>.</param>
		/// <param name="execute">Whether the new interaction should immediately be executed by all involved right after its set up. Warning: if any of the attempts return false, this method will also return false.</param>
		/// <param name="interaction">The resulting <see cref="IInteraction"/> object.</param>
		/// <param name="interactables">The <see cref="IInteractingComponent"/>s involved with the interaction besides this one.</param>
		/// <returns>Whether we were successful in setting up the interaction.</returns>
		bool Attempt(string interactionType, object data, bool execute, out IInteraction interaction, params IInteractingComponent[] interactables);
	}
}
