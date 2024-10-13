using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract entity component with a base implementation for <see cref="IInteractable"/>.
	/// </summary>
	public abstract class InteractableBase : EntityComponentBase, IInteractable
	{
		/// <inheritdoc/>
		public event Action<IInteraction> InteractableEvent;

		/// <inheritdoc/>
		public virtual Vector3 InteractablePoint => targetable == null ? transform.position : targetable.Center;

		/// <inheritdoc/>
		public virtual float InteractableRange => targetable == null ? 0f : targetable.Size.Average();

		/// <inheritdoc/>
		public virtual bool Interactable { get; protected set; } = true;

		/// <inheritdoc/>
		public abstract string[] InteractableTypes { get; protected set; }

		protected IDependencyManager dependencyManager;
		protected ITargetable targetable;

		public void InjectDependencies(IDependencyManager dependencyManager)
		{
			this.dependencyManager = dependencyManager;

			// Targetable is optional, therefore retrieve manually.
			targetable = dependencyManager.Get<ITargetable>(true, false);
		}

		/// <inheritdoc/>
		public abstract bool IsInteractable(string interactionType);

		/// <inheritdoc/>
		public bool TryInteract(IInteraction interaction)
		{
			if (Interactable && IsInteractable(interaction.Type) && OnTryInteract(interaction))
			{
				InteractableEvent?.Invoke(interaction);
				return true;
			}

			//interaction.Conclude(false);
			return false;
		}

		/// <summary>
		/// Called from <see cref="TryInteract(IInteraction)"/> if interaction type is supported.
		/// </summary>
		protected abstract bool OnTryInteract(IInteraction interaction);
	}
}
