using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Base implementation of an <see cref="IInteraction"/>.
	/// </summary>
	public class Interaction : IInteraction
	{
		/// <inheritdoc/>
		public event Action InteractionConcludedEvent;

		/// <inheritdoc/>
		public string Type { get; }

		/// <inheritdoc/>
		public IInteractingComponent Interactor { get; }

		/// <inheritdoc/>
		public List<IInteractingComponent> Interactables { get; }

		/// <inheritdoc/>
		public object Data { get; }

		/// <inheritdoc/>
		public bool Concluded { get; private set; }

		public Interaction(string type, IInteractingComponent interactor, List<IInteractingComponent> interactables, object data = null)
		{
			Type = type;
			Interactor = interactor;
			Interactables = interactables;
			Data = data;
		}

		/// <inheritdoc/>
		public void Conclude()
		{
			Concluded = true;
			InteractionConcludedEvent?.Invoke();
		}
	}
}
