using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract entity component with a base implementation for <see cref="IInteractor"/>.
	/// </summary>
	public abstract class InteractorBase : EntityComponentBase, IInteractor
	{
		/// <inheritdoc/>
		public event Action<IInteraction> InteractorEvent;

		/// <inheritdoc/>
		public virtual Vector3 InteractorPoint => targetable == null ? transform.position : targetable.Center;

		/// <inheritdoc/>
		public virtual float InteractorRange => targetable == null ? 1f : targetable.Size.Max();

		protected ITargetable targetable;

		public void InjectDependencies(IDependencyManager dependencyManager)
		{
			// Targetable is optional, therefore retrieve manually.
			targetable = dependencyManager.Get<ITargetable>(true, false);
		}

		/// <inheritdoc/>
		public abstract bool CanInteract(string interactionType);

		/// <inheritdoc/>
		public bool TryCreateInteraction(string interactionType, IInteractable interactable, out IInteraction interaction, object data = null)
		{
			interaction = null;
			if (CanInteract(interactionType) && interactable.Interactable && CreateInteraction(interactionType, interactable, data, out interaction))
			{
				InteractorEvent?.Invoke(interaction);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Called from <see cref="AttemptInteraction(string, IInteractable, object, out IInteraction)"/> if <see cref="CanInteract(string)"/> and
		/// <paramref name="interactable"/>.<see cref="IInteractable.IsInteractable(IInteractor, string)"/> return true.
		/// </summary>
		protected abstract bool CreateInteraction(string interactionType, IInteractable interactable, object data, out IInteraction interaction);
	}
}
