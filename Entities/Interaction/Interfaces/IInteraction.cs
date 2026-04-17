using System;

namespace SpaxUtils
{
	/// <summary>
	/// Contains data relating to an interaction between <see cref="IInteractionHandler"/>s.
	/// </summary>
	public interface IInteraction : IDisposable
	{
		/// <summary>
		/// Invoked once this interaction has been initiated.
		/// </summary>
		event Action<IInteraction> InitiatedEvent;

		/// <summary>
		/// Invoked once this interaction has been concluded.
		/// </summary>
		event Action<IInteraction> ConcludedEvent;

		/// <summary>
		/// The entity that initiated this interaction.
		/// </summary>
		IEntity Interactor { get; }

		/// <summary>
		/// The <see cref="IInteractable"/> that is being interacted with.
		/// </summary>
		IInteractable Interactable { get; }

		/// <summary>
		/// The action that is to be performed upon the interactable.
		/// </summary>
		string Action { get; }

		/// <summary>
		/// The data object of this interaction, set by either the interactor (key) or interactable (return) depending on interaction.
		/// </summary>
		object Data { get; set; }

		/// <summary>
		/// Whether this interaction has been succesfully initiated between the two entitites.
		/// </summary>
		bool Initiated { get; }

		/// <summary>
		/// Whether this interaction had been concluded or is still ongoing.
		/// </summary>
		bool Concluded { get; }

		/// <summary>
		/// Gets whether the interaction was concluded a success or a failure.
		/// </summary>
		bool Success { get; }

		/// <summary>
		/// The conclusion message containing the reason for its failure, if any.
		/// </summary>
		string Message { get; }

		/// <summary>
		/// Initiates this interaction.
		/// </summary>
		bool TryInitiate();

		/// <summary>
		/// Concludes the interaction either sucessfully or as a failure.
		/// </summary>
		void Conclude(bool success, string message = "");
	}
}
