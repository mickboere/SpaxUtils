using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract entity component with a base implementation for <see cref="IInteractor"/>.
	/// </summary>
	public abstract class InteractorBase : EntityComponentBase, IInteractor
	{
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
		public abstract bool Able(string interactionType);

		/// <inheritdoc/>
		public bool AttemptInteraction(string interactionType, IInteractable interactable, object data, out IInteraction interaction)
		{
			interaction = null;
			if (Able(interactionType) && interactable.Interactable)
			{
				return Attempt(interactionType, interactable, data, out interaction);
			}

			return false;
		}

		/// <summary>
		/// Called from <see cref="AttemptInteraction(string, IInteractable, object, out IInteraction)"/> if <see cref="Able(string)"/> and
		/// <paramref name="interactable"/>.<see cref="IInteractable.IsInteractable(IInteractor, string)"/> return true.
		/// </summary>
		protected abstract bool Attempt(string interactionType, IInteractable interactable, object data, out IInteraction interaction);
	}
}
