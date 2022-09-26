namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects able to initiate <see cref="IInteraction"/>s with <see cref="IInteractable"/>s.
	/// </summary>
	public interface IInteractor : IEntityComponent
	{
		/// <summary>
		/// Whether this handler is able to set up a new interaction of type <paramref name="interactionType"/>.
		/// </summary>
		/// <param name="interactionType"></param>
		/// <returns></returns>
		bool Able(string interactionType);

		/// <summary>
		/// Will try to set up a new interaction of type <paramref name="interactionType"/>.
		/// </summary>
		/// <param name="interactable">The <see cref="IInteractable"/> this interactor should attempt interaction with.</param>
		/// <param name="interactionType">The type of interaction to try and set up.</param>
		/// <param name="data">The <see cref="IInteraction.Data"/>.</param>
		/// <param name="interaction">The resulting <see cref="IInteraction"/> object.</param>
		/// <returns>Whether we were successful in setting up the interaction.</returns>
		bool Attempt(string interactionType, IInteractable interactable, object data, out IInteraction interaction);
	}
}
