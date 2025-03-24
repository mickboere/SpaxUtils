using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract non-component <see cref="IInteractable"/> implementation.
	/// </summary>
	public abstract class InteractableBase : EntityComponent, IInteractable
	{
		/// <inheritdoc/>
		public abstract string InteractableType { get; }

		/// <inheritdoc/>
		public virtual bool IsInteractable => true;

		public InteractableBase(IEntity entity, IDependencyManager dependencyManager) : base(entity, dependencyManager)
		{
		}

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
