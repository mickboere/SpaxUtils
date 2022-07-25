using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Contains data relating to an interaction between <see cref="IInteractingComponent"/>s.
	/// </summary>
	public interface IInteraction
	{
		/// <summary>
		/// Invoked when the interaction has concluded.
		/// </summary>
		event Action InteractionConcludedEvent;

		/// <summary>
		/// The type of interaction.
		/// </summary>
		string Type { get; }

		/// <summary>
		/// The <see cref="IInteractingComponent"/> that instigated this interaction.
		/// </summary>
		IInteractingComponent Interactor { get; }

		/// <summary>
		/// The <see cref="IInteractingComponent"/>s that are being interacted with.
		/// </summary>
		List<IInteractingComponent> Interactables { get; }

		/// <summary>
		/// The interaction data object.
		/// </summary>
		object Data { get; }

		/// <summary>
		/// Is the interaction concluded?
		/// </summary>
		bool Concluded { get; }

		/// <summary>
		/// Conclude the interaction.
		/// </summary>
		void Conclude();
	}
}
