using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects able to be interacted with by <see cref="IInteractor"/>s.
	/// </summary>
	public interface IInteractable : IEntityComponent
	{
		/// <summary>
		/// All of the interaction types this handler is interactable with.
		/// </summary>
		IReadOnlyList<string> InteractableTypes { get; }

		/// <summary>
		/// Returns true if this interactable supports <paramref name="interactionType"/>
		/// and is allowed to be interacted with by <paramref name="interactor"/>.
		/// </summary>
		bool Interactable(IInteractor interactor, string interactionType);

		/// <summary>
		/// Try to interact with this object using <paramref name="interaction"/>.
		/// </summary>
		/// <param name="interaction">The interaction to attempt.</param>
		/// <returns>TRUE when succeeded, FALSE when failed.</returns>
		bool Interact(IInteraction interaction);
	}
}
