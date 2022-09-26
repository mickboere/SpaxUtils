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
	public class InteractingService : IService, IDisposable
	{
		private EntityComponentFilter<IInteractionHandler> interactables;

		public InteractingService(IEntityCollection entityCollection)
		{
			interactables = new EntityComponentFilter<IInteractionHandler>(entityCollection);
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
		/// <param name="layerMask">The layermask used when checking if the interaction between the interactor and interactable is obstructed.</param>
		/// <returns></returns>
		public bool GetInteractablesInRange(IInteractionHandler interactor, out List<(IInteractionHandler interactable, float distance)> results, string interactionType = "", int layerMask = ~0)
		{
			results = new List<(IInteractionHandler interactable, float distance)>();

			// Collect all consenting interactables within each other's range.
			foreach (IInteractionHandler component in interactables.Components)
			{
				// Skip the interactor ofcourse
				if (component == interactor)
				{
					continue;
				}

				// Ask interactable for consent.
				if (component.Interactable(interactor, interactionType))
				{
					// TODO: Develop "InteractionBridge" for needing to interact over a counter or some other gap over which interaction should still be possible.
					//		Bridged interactions don't need to check for raycasts.
					//		This service keeps track of bridges using an entity filter.
					// ELSE:
					// Check if interacting components are within each other's range.
					float dist = Vector3.Distance(interactor.InteractionPoint, component.InteractionPoint);
					if (dist < interactor.InteractionRange + component.InteractionRange)
					{
						results.Add((component, dist));
					}
				}
			}

			// Sort consenting interactables in range by distance.
			results.Sort((x, y) => x.distance.CompareTo(y.distance));

			// Cast a ray from the interactor to the interactable to see if nothing is blocking them.
			// Remove all interactions that are obstructed from the results.
			for (int i = 0; i < results.Count; i++)
			{
				if (results[i].distance > interactor.InteractionRange + results[i].interactable.InteractionRange)
				{
					// Bridged components don't need to check for obstruction.
					continue;
				}


				RaycastHit[] hits = Physics.RaycastAll(interactor.InteractionPoint, Vector3.Normalize(results[i].interactable.InteractionPoint - interactor.InteractionPoint), results[i].distance, layerMask);
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
		public bool GetClosestInteractable(IInteractionHandler interactor, out IInteractionHandler interactable, out float distance, string interactionType = "", int layerMask = ~0)
		{
			interactable = null;
			distance = 0f;

			if (GetInteractablesInRange(interactor, out List<(IInteractionHandler interactable, float distance)> results, interactionType, layerMask))
			{
				interactable = results[0].interactable;
				distance = results[0].distance;
			}

			return interactable != null;
		}
	}
}
