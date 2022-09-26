using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> implementing <see cref="IInteractionHandler"/> that can handle <see cref="IInteraction"/>s by passing them to appropriate .
	/// </summary>
	public class InteractionHandlerComponent : EntityComponentBase, IInteractionHandler
	{
		/// <inheritdoc/>
		public event Action<IInteraction> EnteredInteractionEvent;

		/// <inheritdoc/>
		public IReadOnlyList<string> InteractableTypes => interactables.SelectMany(h => h.InteractableTypes).ToList();

		public virtual Vector3 InteractionPoint => targetable != null ? targetable.Center : Entity.GameObject.transform.position;
		public virtual float InteractionRange => targetable != null ? targetable.Size.Max() * 0.75f : 1.5f;

		protected IList<IInteractor> interactors;
		protected IList<IInteractable> interactables;
		protected IList<IInteractionBlocker> blockers;

		private ITargetable targetable;

		public void InjectDependencies(IInteractor[] interactors, IInteractable[] interactables, IInteractionBlocker[] blockers, ITargetable targetable)
		{
			this.interactors = new List<IInteractor>(interactors);
			this.interactors.Remove(this);
			this.interactables = new List<IInteractable>(interactables);
			this.interactables.Remove(this);
			this.blockers = new List<IInteractionBlocker>(blockers);
			this.targetable = targetable;
		}

		/// <inheritdoc/>
		public bool Interactable(IInteractor interactor, string interactionType)
		{
			// Check for blockers.
			if (GetBlockers(interactionType).Count > 0)
			{
				return false;
			}

			// Ask consent of each handler until one accepts.
			foreach (IInteractable interactable in interactables)
			{
				if (interactable.Interactable(interactor, interactionType))
				{
					return true;
				}
			}

			return false;
		}

		/// <inheritdoc/>
		public bool Interact(IInteraction interaction)
		{
			if (!Interactable(interaction.Interactor, interaction.Type))
			{
				return false;
			}

			// Attempt the interaction with each handler until one succeeds.
			foreach (IInteractable interactable in interactables)
			{
				if (interactable.Interact(interaction))
				{
					return true;
				}
			}

			return false;
		}

		/// <inheritdoc/>
		public bool Able(string interactionType)
		{
			// Check for blockers.
			if (GetBlockers(interactionType).Count > 0)
			{
				return false;
			}

			// Ask of each handler if they are able until one accepts.
			foreach (IInteractor interactor in interactors)
			{
				if (interactor.Able(interactionType))
				{
					return true;
				}
			}

			return false;
		}

		/// <inheritdoc/>
		public bool Attempt(string interactionType, IInteractable interactable, object data, out IInteraction interaction)
		{
			interaction = null;
			if (!Able(interactionType))
			{
				return false;
			}

			// Ask each handler to set up the interaction until one succeeds.
			foreach (IInteractor interactor in interactors)
			{
				if (interactor.Attempt(interactionType, interactable, data, out interaction))
				{
					return true;
				}
			}

			return false;
		}

		/// <inheritdoc/>
		public void AddInteractor(IInteractor interactor)
		{
			if (!interactors.Contains(interactor))
			{
				interactors.Add(interactor);
			}
		}

		/// <inheritdoc/>
		public void RemoveInteractor(IInteractor interactor)
		{
			if (interactors.Contains(interactor))
			{
				interactors.Remove(interactor);
			}
		}

		/// <inheritdoc/>
		public void AddInteractable(IInteractable interactable)
		{
			if (!interactables.Contains(interactable))
			{
				interactables.Add(interactable);
			}
		}

		/// <inheritdoc/>
		public void RemoveInteractable(IInteractable interactable)
		{
			if (interactables.Contains(interactable))
			{
				interactables.Remove(interactable);
			}
		}

		/// <inheritdoc/>
		public void AddBlocker(IInteractionBlocker interactionBlocker)
		{
			if (!blockers.Contains(interactionBlocker))
			{
				blockers.Add(interactionBlocker);
			}
		}

		/// <inheritdoc/>
		public void RemoveBlocker(IInteractionBlocker interactionBlocker)
		{
			if (blockers.Contains(interactionBlocker))
			{
				blockers.Remove(interactionBlocker);
			}
		}

		/// <inheritdoc/>
		public IList<IInteractionBlocker> GetBlockers(string interactionType)
		{
			return blockers.Where(b => (b.BlocksTypes == null || b.BlocksTypes.Count == 0 || b.BlocksTypes.Contains(interactionType)) && b.Blocking).ToList();
		}

		/// <inheritdoc/>
		public bool IsBlocked(string interactionType)
		{
			return GetBlockers(interactionType).Count > 0;
		}
	}
}
