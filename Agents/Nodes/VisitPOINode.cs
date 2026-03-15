using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	/// <summary>
	/// Brain/flow state node that navigates the agent to a specific <see cref="PointOfInterest"/>
	/// and occupies it indefinitely via <see cref="POIHandler"/>.
	/// Unlike <see cref="AgentWanderNode"/>, this node targets one POI and stays.
	/// Intended for NPCs like Brother who wait at a specific location.
	/// </summary>
	[NodeWidth(280)]
	public class VisitPOINode : StateComponentNodeBase
	{
		[Header("Target")]
		[SerializeField, ConstDropdown(typeof(IPOITags)), Tooltip("Only visit POIs with all of these tags.")]
		private string[] poiTags;

		[Header("Navigation")]
		[SerializeField, Tooltip("Movement speed passed to navigation.")]
		private float moveSpeed = 0.5f;

		[SerializeField, Tooltip("Arrival distance to consider the POI reached.")]
		private float arrivalRange = 0.25f;

		private enum VisitActivity { Selecting, Moving, Occupied }

		private IAgent agent;
		private AgentNavigationHandler navigation;
		private CallbackService callbackService;
		private WorldRegionService worldRegionService;
		private POIHandler poiHandler;
		private ISpawnpoint spawnpoint;

		private bool initialized;
		private VisitActivity activity;
		private PointOfInterest currentPOI;
		private Vector3 currentDestination;

		public void InjectDependencies(
			IAgent agent,
			AgentNavigationHandler navigation,
			CallbackService callbackService,
			WorldRegionService worldRegionService,
			POIHandler poiHandler,
			[Optional] ISpawnpoint spawnpoint)
		{
			this.agent = agent;
			this.navigation = navigation;
			this.callbackService = callbackService;
			this.worldRegionService = worldRegionService;
			this.poiHandler = poiHandler;
			this.spawnpoint = spawnpoint;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			if (!initialized)
			{
				initialized = true;
				currentPOI = null;
				activity = VisitActivity.Selecting;
			}
			else if (poiHandler.IsOccupying && poiHandler.CurrentPOI == currentPOI)
			{
				// POIHandler still holds our POI (Passive scope kept it alive
				// through Idle/Interacting transition). Resume occupied.
				activity = VisitActivity.Occupied;
			}
			else
			{
				// POI was vacated (e.g. Combat ended, Passive exit cleared it).
				// Or first entry after initialization. Navigate to it again.
				currentPOI = null;
				activity = VisitActivity.Selecting;
			}

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
			navigation.ResetInput();

			// Do NOT vacate. POIHandler persists in Passive scope.
			// AnimatedActionsNode handles vacating on Passive exit.
		}

		private void OnUpdate(float delta)
		{
			switch (activity)
			{
				case VisitActivity.Selecting:
					TrySelectPOI();
					break;

				case VisitActivity.Moving:
					if (navigation.MoveInRange(arrivalRange, moveSpeed, true, currentDestination))
					{
						TryOccupyOnArrival();
					}
					break;

				case VisitActivity.Occupied:
					// Do nothing. Agent stays at POI.
					// AnimatedActionsNode in Passive handles the action.
					break;
			}
		}

		private bool TrySelectPOI()
		{
			IWorldRegion region = null;
			PointOfInterest poi = null;

			if (spawnpoint != null && spawnpoint.Region != null)
			{
				region = spawnpoint.Region;
			}
			else
			{
				region = worldRegionService.GetRegion(agent.Transform.position);
			}

			if (region == null)
			{
				return false;
			}

			// Search by tags within the agent's region.
			List<PointOfInterest> available = region.GetAvailablePOIs(poiTags);
			if (available.Count > 0)
			{
				// Pick the closest available POI.
				float bestDist = float.MaxValue;
				Vector3 agentPos = agent.Transform.position;
				for (int i = 0; i < available.Count; i++)
				{
					float dist = Vector3.Distance(agentPos, available[i].transform.position);
					if (dist < bestDist)
					{
						bestDist = dist;
						poi = available[i];
					}
				}
			}

			if (poi == null)
			{
				return false;
			}

			// No reservation. Just navigate toward it. Occupation happens on arrival.
			// Navigate directly to the POI's position; the navigation system will
			// pathfind to the nearest reachable point on the NavMesh.
			currentPOI = poi;
			currentDestination = poi.transform.position;
			activity = VisitActivity.Moving;

			return true;
		}

		private void TryOccupyOnArrival()
		{
			navigation.ResetInput();

			if (currentPOI != null && currentPOI.TryOccupy(agent))
			{
				// Align agent with POI transform.
				agent.Transform.position = currentPOI.transform.position;
				agent.Transform.rotation = currentPOI.transform.rotation;

				// Write to POIHandler so AnimatedActionsNode can pick up the action.
				poiHandler.Occupy(currentPOI);
				activity = VisitActivity.Occupied;
			}
			else
			{
				// Someone else got there first. Reselect.
				currentPOI = null;
				activity = VisitActivity.Selecting;
			}
		}
	}
}
