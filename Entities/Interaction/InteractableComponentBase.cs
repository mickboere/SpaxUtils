using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract entity component with a base implementation for <see cref="IInteractable"/>.
	/// </summary>
	public abstract class InteractableComponentBase : EntityComponentMono, IInteractable
	{
		/// <inheritdoc/>
		public abstract string InteractableType { get; }

		/// <inheritdoc/>
		public virtual bool IsInteractable => true;

		/// <inheritdoc/>
		public virtual List<string> GetInteractions(IEntity interactor)
		{
			return new List<string>();
		}

		/// <inheritdoc/>
		public virtual bool TryInteract(IInteraction interaction)
		{
			return true;
		}
	}
}
