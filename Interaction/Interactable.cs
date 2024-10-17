using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract non-component class with a base implementation for <see cref="IInteractable"/>.
	/// </summary>
	public abstract class Interactable : EntityComponent, IInteractable
	{
		/// <inheritdoc/>
		public event Action<IInteraction> InteractableEvent;

		/// <inheritdoc/>
		public virtual Vector3 InteractablePoint => targetable == null ? Transform.position : targetable.Center;

		/// <inheritdoc/>
		public virtual float InteractableRange => targetable == null ? 0f : targetable.Size.Average();

		/// <inheritdoc/>
		public abstract string[] InteractableTypes { get; protected set; }

		protected ITargetable targetable;

		public Interactable(IEntity entity, IDependencyManager dependencyManager) : base(entity, dependencyManager)
		{
			// Targetable is optional, therefore retrieve manually.
			targetable = entity.GetEntityComponent<ITargetable>();
		}

		/// <inheritdoc/>
		public abstract bool IsInteractable(string interactionType);

		/// <inheritdoc/>
		public bool TryInteract(IInteraction interaction)
		{
			if (IsInteractable(interaction.Type) && OnTryInteract(interaction))
			{
				InteractableEvent?.Invoke(interaction);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Called from <see cref="TryInteract(IInteraction)"/> if interaction type is supported.
		/// </summary>
		protected abstract bool OnTryInteract(IInteraction interaction);
	}
}
