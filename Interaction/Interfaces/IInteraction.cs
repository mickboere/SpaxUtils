using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Contains data relating to an interaction between <see cref="IInteractionHandler"/>s.
	/// </summary>
	public interface IInteraction : IDisposable
	{
		/// <summary>
		/// Invoked when the interaction has concluded.
		/// The bool indicates whether the interaction was a success or a failure.
		/// </summary>
		event Action<IInteraction, bool> ConcludedEvent;

		/// <summary>
		/// The type of interaction.
		/// </summary>
		string Type { get; }

		/// <summary>
		/// The <see cref="IInteractor"/> that initiated this interaction.
		/// </summary>
		IInteractor Interactor { get; }

		/// <summary>
		/// The <see cref="IInteractable"/> that is being interacted with.
		/// </summary>
		IInteractable Interactable { get; }

		/// <summary>
		/// The interaction data object.
		/// </summary>
		object Data { get; set; }

		/// <summary>
		/// Gets whether the interaction was concluded or is still ongoing.
		/// </summary>
		bool Concluded { get; }

		/// <summary>
		/// Gets whether the interaction was concluded a success or a failure.
		/// </summary>
		bool Success { get; }

		/// <summary>
		/// Concludes the interaction either sucessfully or as a failure.
		/// </summary>
		void Conclude(bool success);
	}
}
