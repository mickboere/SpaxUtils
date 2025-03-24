using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> handling all interactions for an entity.
	/// </summary>
	public class InteractionHandler : EntityComponentMono
	{
		/// <summary>
		/// Event invoked when an interaction has been established between two entities.
		/// </summary>
		public event Action<IInteraction> InteractionEvent;

		public IReadOnlyList<IInteraction> Interactions => interactions;
		public Vector3 InteractionPoint => targetable == null ? Entity.Transform.position : targetable.Center;
		public float InteractionRange => targetable == null ? defaultInteractionRange : targetable.Size.Average() * 0.5f;

		private List<IInteractable> interactables = new List<IInteractable>();
		private List<IInteractor> interactors = new List<IInteractor>();
		private List<IInteractionBlocker> blockers;
		private List<IInteraction> interactions = new List<IInteraction>();

		[SerializeField, Tooltip("Picked if there is no targetable attached to the entity.")] private float defaultInteractionRange = 1f;

		private ITargeter targeter;
		private ITargetable targetable;

		public void InjectDependencies(IInteractable[] interactables, IInteractor[] interactors, IInteractionBlocker[] blockers,
			[Optional] ITargeter targeter, [Optional] ITargetable targetable)
		{
			foreach (IInteractable interactable in interactables)
			{
				AddInteractable(interactable);
			}
			foreach (IInteractor interactor in interactors)
			{
				AddInteractor(interactor);
			}
			this.blockers = new List<IInteractionBlocker>(blockers);
			this.targeter = targeter;
			this.targetable = targetable;
		}

		/// <summary>
		/// Returns whether any child interactables are of interaction type <paramref name="type"/>.
		/// </summary>
		public bool HasInteractableType(string type)
		{
			return interactables.Any((i) => i.InteractableType == type);
		}

		/// <summary>
		/// Returns a list of available interaction options that when picked will initiate the selected interaction.
		/// </summary>
		public List<Option> GetInteractableOptions(InteractionHandler interactor)
		{
			List<Option> result = new List<Option>();
			foreach (IInteractable interactable in interactables)
			{
				if (!interactable.IsInteractable ||
					IsBlocked(interactable.InteractableType) ||
					interactor.IsBlocked(interactable.InteractableType))
				{
					continue;
				}

				// Add all interactable-based options.
				List<string> actions = interactable.GetInteractions(interactor.Entity);
				foreach (string action in actions)
				{
					result.Add(new Option(action.LastDivision(), "",
						(_) =>
						{
							interactor.CreateInteraction(interactable, action).TryInitiate();
						}));
				}

				// Add all interactor-based options.
				foreach (IInteractor i in interactor.interactors)
				{
					actions = i.GetInteractions(interactable);
					foreach (string action in actions)
					{
						result.Add(new Option(action.LastDivision(), "",
						(_) =>
						{
							interactor.CreateInteraction(interactable, action).TryInitiate();
						}));
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Returns a list of available interaction options that when picked will initiate the selected interaction.
		/// </summary>
		public List<Option> GetInteractorOptions(InteractionHandler interactable)
		{
			return interactable.GetInteractableOptions(this);
		}

		/// <summary>
		/// Create a new interaction with this handler as the interactor, aiming to perform <paramref name="action"/> upon <paramref name="interactable"/>.
		/// </summary>
		/// <param name="interactable">The interactable to interact with.</param>
		/// <param name="action">The action to perform upon the interactable.</param>
		public IInteraction CreateInteraction(IInteractable interactable, string action)
		{
			IInteraction interaction;

			// First check if there is a dedicated interactor.
			foreach (IInteractor interactor in interactors)
			{
				if (interactor.TryCreateInteraction(interactable, action, out interaction))
				{
					interaction.InitiatedEvent += OnInitiatedEvent;
					return interaction;
				}
			}

			// Create default interaction where the interactable is fully responsible for handling the action.
			interaction = new Interaction(Entity, interactable, action);
			interaction.InitiatedEvent += OnInitiatedEvent;
			return interaction;
		}

		/// <summary>
		/// Have this interaction handler attempt to initiate an interaction with <paramref name="interactable"/>.
		/// </summary>
		public bool TryInteract(InteractionHandler interactable, string type, string action, out IInteraction interaction)
		{
			foreach (IInteractable i in interactable.interactables)
			{
				if (i.InteractableType == type)
				{
					interaction = CreateInteraction(i, action);
					return interaction.TryInitiate();
				}
			}

			interaction = null;
			return false;
		}

		#region Interaction object management

		public void AddInteractable(IInteractable interactable)
		{
			if (!interactables.Contains(interactable))
			{
				interactables.Add(interactable);
			}
		}

		public void RemoveInteractable(IInteractable interactable)
		{
			if (interactables.Contains(interactable))
			{
				interactables.Remove(interactable);
			}
		}

		public void AddInteractor(IInteractor interactor)
		{
			if (!interactors.Contains(interactor))
			{
				interactors.Add(interactor);
			}
		}

		public void RemoveInteractor(IInteractor interactor)
		{
			if (interactors.Contains(interactor))
			{
				interactors.Remove(interactor);
			}
		}

		#endregion Interaction object management

		#region Blockers

		public void AddBlocker(IInteractionBlocker interactionBlocker)
		{
			if (!blockers.Contains(interactionBlocker))
			{
				blockers.Add(interactionBlocker);
			}
		}

		public void RemoveBlocker(IInteractionBlocker interactionBlocker)
		{
			if (blockers.Contains(interactionBlocker))
			{
				blockers.Remove(interactionBlocker);
			}
		}

		/// <summary>
		/// Returns all blockers currently blocking interactions with interactables of type <paramref name="type"/>.
		/// </summary>
		public IList<IInteractionBlocker> GetBlockers(string type)
		{
			return blockers.Where(b => (b.BlocksTypes == null || b.BlocksTypes.Count == 0 || b.BlocksTypes.Contains(type)) && b.Blocking).ToList();
		}

		/// <summary>
		/// Returns whether interactions with interactables of type <paramref name="type"/> are currently blocked.
		/// </summary>
		public bool IsBlocked(string type)
		{
			return GetBlockers(type).Count > 0;
		}

		#endregion Blockers

		private void OnInitiatedEvent(IInteraction interaction)
		{
			if (!interaction.Concluded)
			{
				interactions.Add(interaction);
				interaction.ConcludedEvent += OnConcludedEvent;

				if (targeter != null && interaction.Interactable.Entity.TryGetEntityComponent(out ITargetable targetable))
				{
					// Target the interactable.
					targeter.SetTarget(targetable);
				}
			}

			InteractionEvent?.Invoke(interaction);
		}

		private void OnConcludedEvent(IInteraction interaction)
		{
			if (interactions.Contains(interaction))
			{
				interaction.ConcludedEvent -= OnConcludedEvent;
				interactions.Remove(interaction);
				targeter?.SetTarget(null);
			}
		}

		protected void OnDrawGizmos()
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireSphere(InteractionPoint, InteractionRange);
		}
	}
}
