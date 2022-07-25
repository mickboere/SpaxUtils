using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> that can handle <see cref="IInteraction"/>s through its collection of <see cref="IInteractionHandler"/>s.
	/// </summary>
	public class InteractingComponent : EntityComponentBase, IInteractingComponent
	{
		/// <inheritdoc/>
		public event Action<IInteraction> EnteredInteractionEvent;

		/// <inheritdoc/>
		public List<string> InteractableTypes => handlers.SelectMany(h => h.InteractableTypes).ToList();

		/// <inheritdoc/>
		public List<IInteraction> ActiveInteractions => handlers.SelectMany(h => h.ActiveInteractions).ToList();

		public virtual Vector3 InteractionPoint => targetable != null ? targetable.Center : Entity.GameObject.transform.position;
		public virtual float InteractionRange => targetable != null ? targetable.Size.Max() * 0.75f : 1.5f;

		protected IList<IInteractionHandler> handlers;
		protected IList<IInteractionBlocker> blockers;

		private ITargetable targetable;

		public void InjectDependencies(IInteractionHandler[] handlers, IInteractionBlocker[] blockers)
		{
			this.handlers = new List<IInteractionHandler>(handlers);
			if (this.handlers.Contains(this))
			{
				this.handlers.Remove(this);
			}

			this.blockers = new List<IInteractionBlocker>(blockers);

			if (Entity.TryGetEntityComponent(out ITargetable targetable))
			{
				this.targetable = targetable;
			}
		}

		/// <inheritdoc/>
		public bool Interactable(IInteractingComponent interactor, string interactionType)
		{
			// Check for blockers.
			if (GetBlockers(interactionType).Count > 0)
			{
				return false;
			}

			// Ask consent of each handler until one accepts.
			foreach (IInteractionHandler handler in handlers)
			{
				if (handler.Interactable(interactor, interactionType))
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

			// Attempt the interaction with each handler that consents until one succeeds.
			foreach (IInteractionHandler handler in handlers)
			{
				if (handler.Interactable(interaction.Interactor, interaction.Type) && handler.Interact(interaction))
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
			foreach (IInteractionHandler handler in handlers)
			{
				if (handler.Able(interactionType))
				{
					return true;
				}
			}

			return false;
		}

		/// <inheritdoc/>
		public bool Attempt(string interactionType, object data, bool attempt, out IInteraction interaction, params IInteractingComponent[] interactables)
		{
			interaction = null;
			if (!Able(interactionType))
			{
				return false;
			}

			// Ask each able handler to set up the interaction until one succeeds.
			foreach (IInteractionHandler handler in handlers)
			{
				if (handler.Able(interactionType) && handler.Attempt(interactionType, data, attempt, out interaction, interactables))
				{
					return true;
				}
			}

			return false;
		}

		/// <inheritdoc/>
		public IList<IInteractionBlocker> GetBlockers(string interactionType)
		{
			return blockers.Where(b => (b.BlocksTypes == null || b.BlocksTypes.Count == 0 || b.BlocksTypes.Contains(interactionType)) && b.Blocking).ToList();
		}

		/// <inheritdoc/>
		public void AddHandler(IInteractionHandler interactionHandler)
		{
			if (!handlers.Contains(interactionHandler))
			{
				handlers.Add(interactionHandler);
			}
		}

		/// <inheritdoc/>
		public void RemoveHandler(IInteractionHandler interactionHandler)
		{
			if (handlers.Contains(interactionHandler))
			{
				handlers.Remove(interactionHandler);
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
	}
}
