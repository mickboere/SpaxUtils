using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract entity component class implementing both <see cref="IInteractable"/> and <see cref="IInteractor"/>.
	/// </summary>
	public abstract class InteractableInteractorBase : InteractableBase, IInteractor
	{
		/// <inheritdoc/>
		public virtual Vector3 InteractorPoint => InteractablePoint;

		/// <inheritdoc/>
		public virtual float InteractorRange => targetable == null ? 1f : targetable.Size.Max();

		/// <inheritdoc/>
		public abstract bool Able(string interactionType);

		/// <inheritdoc/>
		public bool AttemptInteraction(string interactionType, IInteractable interactable, object data, out IInteraction interaction)
		{
			interaction = null;
			if (Able(interactionType) && interactable.Interactable)
			{
				return OnAttempt(interactionType, interactable, data, out interaction);
			}

			return false;
		}

		/// <summary>
		/// Called from <see cref="AttemptInteraction(string, IInteractable, object, out IInteraction)"/> if <see cref="Able(string)"/> and
		/// <paramref name="interactable"/>.<see cref="IInteractable.Supports(string)"/> return true.
		/// </summary>
		protected abstract bool OnAttempt(string interactionType, IInteractable interactable, object data, out IInteraction interaction);
	}
}
