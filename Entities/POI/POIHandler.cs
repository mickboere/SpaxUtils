using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that tracks the currently occupied <see cref="PointOfInterest"/>.
	/// Written by Idle-scope nodes (WanderNode, VisitPOINode), read by Passive-scope nodes (AnimatedActionsNode).
	/// Provides the handoff contract between movement (Idle) and action execution (Passive).
	/// </summary>
	public class POIHandler : EntityComponentMono
	{
		/// <summary>
		/// Invoked when the occupied POI changes (new POI or null on vacate).
		/// </summary>
		public event Action<POIHandler> OccupationChangedEvent;

		/// <summary>
		/// The currently occupied POI, or null if not occupying any.
		/// </summary>
		public PointOfInterest CurrentPOI { get; private set; }

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

		public void InjectDependencies(IAgent agent)
		{
			this.agent = agent;
		}

		/// <summary>
		/// Occupy the given POI. Vacates any previously occupied POI first.
		/// Does NOT call <see cref="PointOfInterest.TryOccupy"/> - the caller
		/// is expected to have already secured the POI before calling this.
		/// </summary>
		/// <param name="poi">The POI to occupy.</param>
		public void Occupy(PointOfInterest poi)
		{
			if (poi == null)
			{
				SpaxDebug.Error("Cannot occupy a null POI.", "", this);
				return;
			}

			if (CurrentPOI == poi)
			{
				return;
			}

			// Vacate current POI first.
			if (IsOccupying)
			{
				VacateInternal();
			}

			CurrentPOI = poi;
			OccupationChangedEvent?.Invoke(this);

			//SpaxDebug.Log($"{agent.ID} Occupy POI:", poi.GetString());
		}

		/// <summary>
		/// Vacate the currently occupied POI.
		/// </summary>
		public void Vacate()
		{
			if (!IsOccupying)
			{
				return;
			}

			//SpaxDebug.Log($"{agent.ID} Vacate POI:", CurrentPOI.GetString());

			VacateInternal();
			OccupationChangedEvent?.Invoke(this);
		}

		private void VacateInternal()
		{
			if (CurrentPOI != null)
			{
				CurrentPOI.Vacate(agent);
			}

			CurrentPOI = null;
		}

		protected void OnDestroy()
		{
			if (IsOccupying)
			{
				VacateInternal();
			}
		}
	}
}
