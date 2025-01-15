using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract entity component class implementing both <see cref="IInteractable"/> and <see cref="IInteractor"/>.
	/// </summary>
	public abstract class InteractorableComponent : InteractableComponent, IInteractor
	{
		/// <inheritdoc/>
		public event Action<IInteraction> InteractorEvent;

		/// <inheritdoc/>
		public virtual Vector3 InteractorPoint => InteractablePoint;

		/// <inheritdoc/>
		public virtual float InteractorRange => targetable == null ? 1f : targetable.Size.Max();

		/// <inheritdoc/>
		public abstract bool CanInteract(string interactionType);

		/// <inheritdoc/>
		public bool TryCreateInteraction(string interactionType, IInteractable interactable, out IInteraction interaction, object data = null)
		{
			interaction = null;
			if (CanInteract(interactionType) && interactable.IsInteractable(interactionType))
			{
				return OnTryCreateInteraction(interactionType, interactable, out interaction, data);
			}

			return false;
		}

		/// <summary>
		/// Called from <see cref="AttemptInteraction(string, IInteractable, object, out IInteraction)"/> if <see cref="CanInteract(string)"/> and
		/// <paramref name="interactable"/>.<see cref="IInteractable.IsInteractable(string)"/> return true.
		/// </summary>
		protected abstract bool OnTryCreateInteraction(string interactionType, IInteractable interactable, out IInteraction interaction, object data = null);
	}
}
