using SpaxUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpiritAxis
{
	/// <summary>
	/// Service keeping track of all interactable entities, includes functions useful for interaction.
	/// </summary>
	public class InteractionService : IService, IDisposable
	{
		private EntityComponentFilter<IInteractable> interactables;

		public InteractionService(IEntityCollection entityCollection)
		{
			interactables = new EntityComponentFilter<IInteractable>(entityCollection);
		}

		public void Dispose()
		{
			interactables.Dispose();
		}

		/// <summary>
		/// Finds all <see cref="IInteractionHandler"/>s in range of <paramref name="interactor"/>.
		/// </summary>
		/// <param name="interactor">The <see cref="IInteractionHandler"/> that wishes to interact.</param>
		/// <param name="results">The resulting <see cref="IInteractionHandler"/>s that are interactable and in range, if any.</param>
		/// <param name="interactionType">Only check for interactables that can handle interactions of this type. Leave empty to allow for all interaction types.</param>
		/// <param name="raycastLayerMask">The layermask used when checking if the interaction between the interactor and interactable is obstructed.</param>
		/// <returns>Whether any interactables were found within range.</returns>
		public bool GetInteractablesInRange(IInteractor interactor,
			out List<(IInteractable interactable, float distance)> results,
			string interactionType = "", int raycastLayerMask = ~0)
		{
			results = new List<(IInteractable interactable, float distance)>();

			// Collect all consenting interactables within interactor's range.
			foreach (IInteractable interactable in interactables.Components)
			{
				// Skip any interactables belonging to the interactor entity.
				if (interactable == interactor || interactable.Entity == interactor.Entity)
				{
					continue;
				}

				// Ask interactable for consent.
				if (interactable.Interactable && (string.IsNullOrEmpty(interactionType) || interactable.Supports(interactionType)))
				{
					// TODO: Develop "InteractionBridge" for needing to interact over a counter or some other gap over which interaction should still be possible.
					float dist = Vector3.Distance(interactor.InteractorPoint, interactable.InteractablePoint);
					if (dist < interactor.InteractorRange + interactable.InteractableRange)
					{
						results.Add((interactable, dist));
					}
				}
			}

			// Sort consenting interactables in range by distance.
			results.Sort((x, y) => x.distance.CompareTo(y.distance));

			// Cast a ray from the interactor to the interactable to see if nothing is blocking them.
			// Remove all interactions that are obstructed from the results.
			for (int i = 0; i < results.Count; i++)
			{
				if (results[i].distance > interactor.InteractorRange + results[i].interactable.InteractableRange)
				{
					// Bridged components don't need to check for obstruction.
					continue;
				}

				RaycastHit[] hits = Physics.RaycastAll(interactor.InteractorPoint, Vector3.Normalize(results[i].interactable.InteractablePoint - interactor.InteractorPoint), results[i].distance, raycastLayerMask);
				// Check if there are any hits that aren't entities. If there are, the interaction is blocked.
				if (hits.Any(hit => !hit.TryGetComponentInParents(out IEntity entity)))
				{
					// Blocked.
					results.RemoveAt(i);
					i--;
				}
			}

			return results.Count > 0;
		}

		/// <summary>
		/// Returns the <paramref name="interactable"/> closest to the <paramref name="interactor"/> within <paramref name="distance"/>.
		/// </summary>
		/// <param name="interactor">The <see cref="IInteractionHandler"/> that wishes to interact.</param>
		/// <param name="interactable">The resulting closest <see cref="IInteractionHandler"/> that's interactable, if any.</param>
		/// <param name="distance">The distance to the closest <see cref="IInteractionHandler"/> that's interactable, if any.</param>
		/// <param name="interactionType">Only check for interactables that can handle interactions of this type. Leave empty to allow for all interaction types.</param>
		/// <param name="layerMask">The layermask used when checking if the interaction between the interactor and interactable is obstructed.</param>
		public bool GetClosestInteractable(IInteractionHandler interactor, out IInteractable interactable, out float distance, string interactionType = "", int layerMask = ~0)
		{
			interactable = null;
			distance = 0f;

			if (GetInteractablesInRange(interactor, out List<(IInteractable interactable, float distance)> results, interactionType, layerMask))
			{
				interactable = results[0].interactable;
				distance = results[0].distance;
			}

			return interactable != null;
		}
	}
}
