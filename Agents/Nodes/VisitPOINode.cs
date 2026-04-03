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
	/// On re-entry after an interruption (e.g. dialogue), verifies that the currently
	/// occupied POI matches this node's label criteria. If not, vacates and reselects.
	/// Delegates navigation and occupation to <see cref="POIVisitHelper"/>.
	/// </summary>
	[NodeWidth(280)]
	public class VisitPOINode : StateComponentNodeBase
	{
		[Header("Target")]
		[SerializeField, ConstDropdown(typeof(IIdentificationLabels)), Tooltip("Only visit POIs whose entity has all of these labels.")]
		private string[] poiLabels;

		[Header("Navigation")]
		[SerializeField, Tooltip("Movement speed passed to navigation.")]
		private float moveSpeed = 0.5f;

		[SerializeField, Tooltip("Arrival distance to consider the POI reached.")]
		private float arrivalRange = 0.25f;

		[SerializeField, Tooltip("Arrival distance to consider the POI reached.")]
		private bool navMesh;

		private IAgent agent;
		private AgentNavigationHandler navigation;
		private CallbackService callbackService;
		private WorldRegionService worldRegionService;
		private EntityService entityService;
		private POIHandler poiHandler;
		private ISpawnpoint spawnpoint;

		private POIVisitHelper visitHelper;
		private bool needsSelection;

		public void InjectDependencies(
			IAgent agent,
			AgentNavigationHandler navigation,
			CallbackService callbackService,
			WorldRegionService worldRegionService,
			EntityService entityService,
			POIHandler poiHandler,
			[Optional] ISpawnpoint spawnpoint)
		{
			this.agent = agent;
			this.navigation = navigation;
			this.callbackService = callbackService;
			this.worldRegionService = worldRegionService;
			this.entityService = entityService;
			this.poiHandler = poiHandler;
			this.spawnpoint = spawnpoint;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			if (poiHandler.IsOccupying)
			{
				PointOfInterest occupiedPOI = poiHandler.CurrentPOI;
				bool labelsMatch = poiLabels == null
					|| poiLabels.Length == 0
					|| occupiedPOI.Entity.Identification.HasAll(poiLabels);

				if (labelsMatch)
				{
					// Currently occupied POI matches our criteria. Resume occupied.
					needsSelection = false;
				}
				else
				{
					// Occupied POI does not match this node's labels. Vacate and reselect.
					poiHandler.Vacate();
					needsSelection = true;
				}
			}
			else
			{
				needsSelection = true;
			}

			if (needsSelection && (agent.Age < 5f || agent.Priority == PriorityLevel.Culled))
			{
				// Try to immediately occupy if just freshly spawned or if culled.
				TrySelectAndVisit(true);
			}

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
			navigation.ResetInput();

			// Stop navigation but do NOT vacate. POIHandler persists in Passive scope.
			// AnimatedActionsNode handles vacating on Passive exit.
			if (visitHelper != null)
			{
				visitHelper.Dispose();
				visitHelper = null;
			}
		}

		private void OnUpdate(float delta)
		{
			// If the helper failed (POI taken by someone else), discard and reselect.
			if (visitHelper != null && visitHelper.HasFailed)
			{
				visitHelper.Dispose();
				visitHelper = null;
				needsSelection = true;
			}

			// If not occupying and no helper active, try to select a POI.
			if (needsSelection && visitHelper == null && !poiHandler.IsOccupying)
			{
				TrySelectAndVisit(false);
			}
		}

		private bool TrySelectAndVisit(bool immediate)
		{
			PointOfInterest poi = SelectPOI();
			if (poi == null)
			{
				return false;
			}

			if (visitHelper != null)
			{
				visitHelper.Dispose();
			}

			visitHelper = new POIVisitHelper(agent, navigation, entityService, poiHandler, callbackService);
			if (visitHelper.Visit(poi, immediate, moveSpeed, arrivalRange))
			{
				needsSelection = false;
				return true;
			}

			// Visit failed immediately (e.g. POI occupied).
			visitHelper.Dispose();
			visitHelper = null;
			return false;
		}

		private PointOfInterest SelectPOI()
		{
			IWorldRegion region = null;

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
				return null;
			}

			// Search by labels within the agent's region.
			List<PointOfInterest> available = region.GetAvailablePOIs(poiLabels);
			if (available.Count == 0)
			{
				return null;
			}

			// Pick the closest available POI.
			PointOfInterest best = null;
			float bestDist = float.MaxValue;
			Vector3 agentPos = agent.Transform.position;
			for (int i = 0; i < available.Count; i++)
			{
				float dist = Vector3.Distance(agentPos, available[i].transform.position);
				if (dist < bestDist)
				{
					bestDist = dist;
					best = available[i];
				}
			}

			return best;
		}
	}
}
