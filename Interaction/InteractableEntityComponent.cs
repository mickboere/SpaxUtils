using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IInteractionHandler"/> implementation that accepts all interactions its not already "busy" with.
	/// Does not engage in any interactions itself, only accepts and keeps track of them.
	/// </summary>
	public class InteractableEntityComponent : EntityComponentBase, IInteractionHandler
	{
		[SerializeField, ConstDropdown(typeof(IInteractionTypeConstants))] private List<string> interactableTypes;

		private Dictionary<string, IInteraction> interactions = new Dictionary<string, IInteraction>();

		public event Action<IInteraction> EnteredInteractionEvent;

		/// <inheritdoc/>
		public List<string> InteractableTypes => interactableTypes.Where(t => Interactable(null, t)).ToList();

		/// <inheritdoc/>
		public List<IInteraction> ActiveInteractions => interactions.Values.Where((i) => !i.Concluded).ToList();

		/// <inheritdoc/>
		public bool Interactable(IInteractingComponent interactor, string interactionType)
		{
			// Check if we're already busy with an interaction of this type
			if (interactions.ContainsKey(interactionType) && !interactions[interactionType].Concluded)
			{
				return false;
			}

			// Check if we're allowed to consume this type of interaction.
			if (interactableTypes.Contains(interactionType) || (interactableTypes.Count > 0 && string.IsNullOrEmpty(interactionType)))
			{
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public bool Interact(IInteraction interaction)
		{
			if (Interactable(interaction.Interactor, interaction.Type))
			{
				// We consented, all we have to do now is store the interaction and return a success.
				interactions[interaction.Type] = interaction;
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public bool Able(string interactionType = "")
		{
			return false;
		}

		/// <inheritdoc/>
		public bool Attempt(string interactionType, object data, bool attempt, out IInteraction interaction, params IInteractingComponent[] interactables)
		{
			interaction = null;
			return false;
		}
	}
}
