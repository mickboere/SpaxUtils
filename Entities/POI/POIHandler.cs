using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that tracks the currently occupied <see cref="PointOfInterest"/>.
	/// Written by Idle-scope nodes (WanderNode, VisitPOINode), read by Passive-scope nodes (AnimatedActionsNode).
	/// Provides the handoff contract between movement (Idle) and action execution (Passive).
	/// Handles alignment and kinematic state on occupy/vacate so callers only need to
	/// call <see cref="Occupy"/> after securing the POI.
	/// Persists the occupied POI entity ID in the agent's <see cref="RuntimeDataCollection"/>
	/// so occupation survives save/load cycles.
	/// </summary>
	public class POIHandler : EntityComponentMono
	{
		private const string DATA_OCCUPIED_POI_ID = "poi.occupiedId";

		/// <summary>
		/// Invoked when the occupied POI changes (new POI or null on vacate).
		/// </summary>
		public event Action<POIHandler> OccupationChangedEvent;

		/// <summary>
		/// The currently occupied POI, or null if not occupying any.
		/// On first access after a save/load cycle, attempts to restore occupation
		/// from the agent's persisted data.
		/// </summary>
		public PointOfInterest CurrentPOI
		{
			get
			{
				TryRestoreFromSave();
				return currentPOI;
			}
		}

		/// <summary>
		/// The action identifier of the currently occupied POI, or null.
		/// Shorthand for <see cref="CurrentPOI"/>.<see cref="PointOfInterest.ActionKey"/>.
		/// </summary>
		public string CurrentActionIdentifier => CurrentPOI != null ? CurrentPOI.ActionKey : null;

		/// <summary>
		/// Whether the agent is currently occupying a POI.
		/// </summary>
		public bool IsOccupying => CurrentPOI != null;

		private IAgent agent;
		private EntityService entityService;
		private AgentNavigationHandler navigation;
		private RigidbodyWrapper rigidbodyWrapper;
		private PointOfInterest currentPOI;
		private bool restoredFromSave;

		public void InjectDependencies(IAgent agent, EntityService entityService,
			AgentNavigationHandler navigation, RigidbodyWrapper rigidbodyWrapper)
		{
			this.agent = agent;
			this.entityService = entityService;
			this.navigation = navigation;
			this.rigidbodyWrapper = rigidbodyWrapper;
			agent.OnSaveEvent += OnSave;
		}

		/// <summary>
		/// Occupy the given POI. Vacates any previously occupied POI first.
		/// Aligns the agent to the POI transform and makes the rigidbody kinematic.
		/// Does NOT call <see cref="PointOfInterest.TryOccupy"/> - the caller
		/// is expected to have already secured the POI before calling this.
		/// </summary>
		/// <param name="poi">The POI to occupy.</param>
		public void Occupy(PointOfInterest poi)
		{
			// Any explicit operation supersedes save restore.
			restoredFromSave = true;

			if (poi == null)
			{
				SpaxDebug.Error("Cannot occupy a null POI.", "", this);
				return;
			}

			if (currentPOI == poi)
			{
				return;
			}

			// Vacate current POI first.
			if (currentPOI != null)
			{
				VacateInternal();
			}

			currentPOI = poi;
			ApplyOccupation();
			OccupationChangedEvent?.Invoke(this);
		}

		/// <summary>
		/// Vacate the currently occupied POI.
		/// Restores the rigidbody to non-kinematic.
		/// </summary>
		public void Vacate()
		{
			// Any explicit operation supersedes save restore.
			restoredFromSave = true;

			if (currentPOI == null)
			{
				return;
			}

			VacateInternal();
			OccupationChangedEvent?.Invoke(this);
		}

		private void ApplyOccupation()
		{
			navigation.ForceAlign(currentPOI.transform.position, currentPOI.transform.forward);
			rigidbodyWrapper.IsKinematic.AddBool(this, true);
		}

		private void VacateInternal()
		{
			rigidbodyWrapper.IsKinematic.RemoveBool(this);

			if (currentPOI != null)
			{
				currentPOI.Vacate(agent);
			}

			currentPOI = null;
		}

		/// <summary>
		/// Lazily restores occupation from saved RuntimeData on first property access.
		/// Deferred to first access so that all scene entities have time to register
		/// in the <see cref="EntityService"/> before we try to resolve the POI ID.
		/// </summary>
		private void TryRestoreFromSave()
		{
			if (restoredFromSave)
			{
				return;
			}

			restoredFromSave = true;

			if (agent.RuntimeData.TryGetValue<string>(DATA_OCCUPIED_POI_ID, out string poiId)
				&& !string.IsNullOrEmpty(poiId))
			{
				if (entityService.TryGet<IEntity>(poiId, out IEntity poiEntity)
					&& poiEntity.TryGetEntityComponent<PointOfInterest>(out PointOfInterest poi))
				{
					if (poi.TryOccupy(agent))
					{
						currentPOI = poi;
						ApplyOccupation();
						OccupationChangedEvent?.Invoke(this);
					}
				}
			}
		}

		private void OnSave(RuntimeDataCollection data)
		{
			string poiId = currentPOI != null ? currentPOI.Entity.ID : "";
			agent.RuntimeData.SetValue(DATA_OCCUPIED_POI_ID, poiId);
		}

		protected void OnDestroy()
		{
			if (agent != null)
			{
				agent.OnSaveEvent -= OnSave;
			}

			if (currentPOI != null)
			{
				VacateInternal();
			}
		}
	}
}
