using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	/// <summary>
	/// Brain node that drives passive wandering behaviour.
	/// Supports three activity modes: navigate to a random point within a WorldRegion,
	/// navigate to a random point within a radius, or navigate to a PointOfInterest.
	/// Rolls against configurable weights each query interval to decide what to do next.
	/// Persists its task state in the agent's <see cref="RuntimeDataCollection"/> so that
	/// wandering survives flow destruction (e.g. entering dialogue) and save/load cycles.
	/// </summary>
	[NodeWidth(280)]
	public class AgentWanderNode : StateComponentNodeBase
	{
		// Large sample range so NavMesh.SamplePosition succeeds across big flat regions.
		private const float NAVMESH_SAMPLE_RANGE = 25f;

		// Maximum weighted selection attempts before giving up and reverting to Idle.
		private const int MAX_SELECTION_ATTEMPTS = 5;

		private const string DATA_WANDER = "wander";
		private const string DATA_ACTIVITY = "activity";
		private const string DATA_DESTINATION = "destination";
		private const string DATA_TARGET_POI_ID = "targetPOIId";
		private const string DATA_DWELL_TIMER = "dwellTimer";
		private const string DATA_QUERY_TIMER = "queryTimer";

		[Header("Timing")]
		[SerializeField, Tooltip("Seconds between activity decisions.")]
		private float queryInterval = 6f;

		[SerializeField, Tooltip("Movement speed passed to navigation.")]
		private float moveSpeed = 0.5f;

		[SerializeField, Tooltip("Arrival distance to consider a destination reached.")]
		private float arrivalRange = 0.25f;

		[Header("Activity Weights")]
		[SerializeField, Range(0f, 1f), Tooltip("Weight for picking a random point within the assigned WorldRegion.")]
		private float regionWanderWeight = 1f;

		[SerializeField, Range(0f, 1f), Tooltip("Weight for picking a random point within wanderRadius of current position.")]
		private float radiusWanderWeight = 1f;

		[SerializeField, Range(0f, 1f), Tooltip("Weight for navigating to a PointOfInterest.")]
		private float poiWeight = 1f;

		[Header("Radius Wander")]
		[SerializeField, Tooltip("Radius around the spawnpoint used when wandering by radius.")]
		private float wanderRadius = 5f;

		[Header("Dwell")]
		[SerializeField] private float dwellTimeMin = 3f;
		[SerializeField] private float dwellTimeMax = 12f;

		[Header("Points of Interest")]
		[SerializeField, ConstDropdown(typeof(IIdentificationLabels)), Tooltip("Only visit POIs whose entity has all of these labels. Leave empty to visit any.")]
		private string[] poiLabels;

		[SerializeField, Range(0f, 1f), Tooltip("How strongly proximity biases POI selection. " +
			"0 = uniform, 1 = strongly prefer nearby. Kept soft by default to avoid always picking the same two nearby POIs.")]
		private float poiDistanceFalloff = 0.33f;

		private IAgent agent;
		private AgentNavigationHandler navigation;
		private CallbackService callbackService;
		private ISpawnpoint spawnpoint;
		private WorldRegionService worldRegionService;
		private POIHandler poiHandler;
		private EntityService entityService;

		private enum WanderActivity { Idle = 0, Moving = 1, MovingRaw = 2, Dwelling = 3 }
		private enum ActivityOption { Region = 0, Radius = 1, POI = 2 }

		private WanderActivity activity;
		private Vector3 currentDestination;
		private PointOfInterest targetPOI;
		private float queryTimer;
		private float dwellTimer;

		// Reused per query to avoid allocation.
		private readonly List<float> activityWeights = new List<float>(3) { 0f, 0f, 0f };

		public void InjectDependencies(
			IAgent agent,
			AgentNavigationHandler navigation,
			CallbackService callbackService,
			WorldRegionService worldRegionService,
			POIHandler poiHandler,
			EntityService entityService,
			[Optional] ISpawnpoint spawnpoint)
		{
			this.agent = agent;
			this.navigation = navigation;
			this.callbackService = callbackService;
			this.worldRegionService = worldRegionService;
			this.poiHandler = poiHandler;
			this.entityService = entityService;
			this.spawnpoint = spawnpoint;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			agent.OnSaveEvent += OnSave;

			if (TryRestoreFromData())
			{
				// Successfully resumed from persisted state.
			}
			else
			{
				// No persisted state or restoration failed. Start fresh.
				activity = WanderActivity.Idle;
				targetPOI = null;
				queryTimer = 0f;
			}

			// If POIHandler holds a POI that this node isn't dwelling at, vacate it.
			// Handles handoff from VisitPOINode or a previous session whose
			// occupation is no longer relevant to this wander.
			if (poiHandler.IsOccupying && poiHandler.CurrentPOI != targetPOI)
			{
				poiHandler.Vacate();
			}

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			SaveToData();

			agent.OnSaveEvent -= OnSave;
			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
			navigation.ResetInput();

			// Do NOT vacate POI here. POIHandler persists in Passive scope.
			// AnimatedActionsNode handles vacating on Passive exit.
			// If we were still moving toward a POI, just forget it - no reservation to release.
			if (activity != WanderActivity.Dwelling)
			{
				targetPOI = null;
			}
		}

		private void OnUpdate(float delta)
		{
			switch (activity)
			{
				case WanderActivity.Idle:
					queryTimer -= delta;
					if (queryTimer <= 0f)
					{
						SelectActivity();
					}
					break;

				case WanderActivity.Moving:
					if (navigation.MoveInRange(arrivalRange, moveSpeed, true, currentDestination))
					{
						if (targetPOI != null)
						{
							// Arrived at POI. Try to occupy now.
							if (targetPOI.TryOccupy(agent))
							{
								poiHandler.Occupy(targetPOI);
								BeginDwell(targetPOI.SampleDwellTime());
							}
							else
							{
								// Someone else beat us to it. Requery.
								targetPOI = null;
								activity = WanderActivity.Idle;
								queryTimer = 0f;
							}
						}
						else
						{
							BeginDwell(Random.Range(dwellTimeMin, dwellTimeMax));
						}
					}
					break;

				case WanderActivity.MovingRaw:
					// Non-NavMesh radius wander. TrySteerWorld handles wall/cliff safety.
					Vector3 toDestination = currentDestination - agent.Transform.position;
					toDestination.y = 0f;

					if (toDestination.magnitude <= arrivalRange)
					{
						BeginDwell(Random.Range(dwellTimeMin, dwellTimeMax));
					}
					else
					{
						navigation.TrySteerWorld(toDestination.normalized * moveSpeed, out bool hardStop);
						if (hardStop)
						{
							// No safe direction exists (cliff all around), give up and requery.
							// Wall deflection (false return but not hardStop) keeps moving in deflected direction.
							activity = WanderActivity.Idle;
							queryTimer = 0f;
						}
					}
					break;

				case WanderActivity.Dwelling:
					dwellTimer -= delta;
					if (dwellTimer <= 0f)
					{
						VacatePOI();
						activity = WanderActivity.Idle;
						queryTimer = 0f;
					}
					break;
			}
		}

		private void SelectActivity()
		{
			queryTimer = queryInterval;

			bool hasRegion = spawnpoint?.Region != null;

			// No region means POI and Region wander are both unavailable; go straight to radius.
			if (!hasRegion)
			{
				TrySelectRadiusPoint();
				return;
			}

			activityWeights[(int)ActivityOption.Region] = regionWanderWeight;
			activityWeights[(int)ActivityOption.Radius] = radiusWanderWeight;
			activityWeights[(int)ActivityOption.POI] = poiWeight;

			for (int attempt = 0; attempt < MAX_SELECTION_ATTEMPTS; attempt++)
			{
				int index = WeightedUtils.RandomIndex(activityWeights);

				switch ((ActivityOption)index)
				{
					case ActivityOption.Region:
						if (TrySelectRegionPoint())
						{
							return;
						}
						break;

					case ActivityOption.Radius:
						if (TrySelectRadiusPoint())
						{
							return;
						}
						break;

					case ActivityOption.POI:
						if (TrySelectPOI())
						{
							return;
						}
						break;
				}
			}

			// All attempts exhausted, remain Idle and try again next interval.
		}

		private bool TrySelectPOI()
		{
			List<PointOfInterest> available = spawnpoint.Region.GetAvailablePOIs(poiLabels);

			if (available.Count == 0)
			{
				return false;
			}

			// Soft inverse-distance weighting. poiDistanceFalloff=0 gives uniform selection,
			// poiDistanceFalloff=1 gives stronger preference for nearby POIs without fully
			// eliminating distant ones.
			float[] weights = new float[available.Count];
			float totalWeight = 0f;
			Vector3 agentPos = agent.Transform.position;

			for (int i = 0; i < available.Count; i++)
			{
				float dist = Vector3.Distance(agentPos, available[i].transform.position);
				float proximity = 1f / (1f + dist);
				weights[i] = Mathf.Lerp(1f, proximity, poiDistanceFalloff);
				totalWeight += weights[i];
			}

			float r = Random.Range(0f, totalWeight);
			float cumulative = 0f;
			PointOfInterest chosen = available[available.Count - 1];

			for (int i = 0; i < available.Count; i++)
			{
				cumulative += weights[i];
				if (r <= cumulative)
				{
					chosen = available[i];
					break;
				}
			}

			// No reservation. Just navigate toward it. Occupation happens on arrival.
			targetPOI = chosen;
			currentDestination = chosen.transform.position;
			activity = WanderActivity.Moving;
			return true;
		}

		private bool TrySelectRegionPoint()
		{
			for (int attempt = 0; attempt < MAX_SELECTION_ATTEMPTS; attempt++)
			{
				Vector3 sample = spawnpoint.Region.SamplePoint();

				// Reject points that fall in a different (higher-priority) region, e.g. an underground area overlapping this one.
				if (worldRegionService.GetRegion(sample) != spawnpoint.Region)
				{
					continue;
				}

				if (NavMesh.SamplePosition(sample, out NavMeshHit hit, NAVMESH_SAMPLE_RANGE, NavMesh.AllAreas))
				{
					// Validate the snapped NavMesh point too, in case it landed outside the region.
					if (worldRegionService.GetRegion(hit.position) != spawnpoint.Region)
					{
						continue;
					}

					targetPOI = null;
					currentDestination = hit.position;
					activity = WanderActivity.Moving;
					return true;
				}
			}

			return false;
		}

		private bool TrySelectRadiusPoint()
		{
			bool hasRegion = spawnpoint?.Region != null;

			for (int attempt = 0; attempt < MAX_SELECTION_ATTEMPTS; attempt++)
			{
				// Case 1 & 2: wander within radius around current position, constrained to region.
				// Case 3: wander within radius around spawnpoint (no region constraint).
				Vector3 center = hasRegion ? agent.Transform.position : spawnpoint.Position;
				Vector3 offset = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward * wanderRadius;
				Vector3 sample = center + offset;

				if (hasRegion && !spawnpoint.Region.IsInside(sample))
				{
					// Sample landed outside the region boundary, try again.
					continue;
				}

				targetPOI = null;

				// Always try NavMesh first regardless of region; only fall back to raw steering if it fails.
				if (NavMesh.SamplePosition(sample, out NavMeshHit hit, NAVMESH_SAMPLE_RANGE, NavMesh.AllAreas))
				{
					// If there is a region, reject points that snap into a different one (e.g. underground overlap).
					if (hasRegion && worldRegionService.GetRegion(hit.position) != spawnpoint.Region)
					{
						continue;
					}

					currentDestination = hit.position;
					activity = WanderActivity.Moving;
					return true;
				}

				// No NavMesh available - use raw steering toward the sampled point.
				currentDestination = sample;
				activity = WanderActivity.MovingRaw;
				return true;
			}

			return false;
		}

		private void BeginDwell(float duration)
		{
			navigation.ResetInput();
			dwellTimer = duration;
			activity = WanderActivity.Dwelling;
		}

		private void VacatePOI()
		{
			if (targetPOI != null)
			{
				// Vacate through POIHandler, which also calls PointOfInterest.Vacate internally.
				poiHandler.Vacate();
				targetPOI = null;
			}
		}

		#region Data Persistence

		private void OnSave(RuntimeDataCollection data)
		{
			SaveToData();
		}

		private void SaveToData()
		{
			RuntimeDataCollection wander = new RuntimeDataCollection(DATA_WANDER);
			wander.SetValue(DATA_ACTIVITY, (int)activity);
			wander.SetValue(DATA_DESTINATION, currentDestination);
			wander.SetValue(DATA_TARGET_POI_ID, targetPOI != null ? targetPOI.Entity.ID : "");
			wander.SetValue(DATA_DWELL_TIMER, dwellTimer);
			wander.SetValue(DATA_QUERY_TIMER, queryTimer);
			agent.RuntimeData.TryAdd(wander, true);
		}

		/// <summary>
		/// Attempts to restore wander state from the agent's persisted <see cref="RuntimeDataCollection"/>.
		/// Returns true if state was successfully restored, false if no saved state exists or restoration failed.
		/// </summary>
		private bool TryRestoreFromData()
		{
			if (!agent.RuntimeData.TryGetEntry<RuntimeDataCollection>(DATA_WANDER, out RuntimeDataCollection wander))
			{
				return false;
			}

			if (!wander.TryGetValue<int>(DATA_ACTIVITY, out int savedActivity))
			{
				return false;
			}

			activity = (WanderActivity)savedActivity;
			currentDestination = wander.GetValue<Vector3>(DATA_DESTINATION);
			queryTimer = wander.GetValue<float>(DATA_QUERY_TIMER);
			dwellTimer = wander.GetValue<float>(DATA_DWELL_TIMER);

			// Resolve target POI if one was saved.
			targetPOI = null;
			string poiId = null;
			if (wander.TryGetValue<string>(DATA_TARGET_POI_ID, out poiId)
				&& !string.IsNullOrEmpty(poiId))
			{
				if (entityService.TryGet<IEntity>(poiId, out IEntity poiEntity)
					&& poiEntity.TryGetEntityComponent<PointOfInterest>(out PointOfInterest poi))
				{
					targetPOI = poi;
				}
			}

			// Validate restored state.
			switch (activity)
			{
				case WanderActivity.Dwelling:
					if (targetPOI != null)
					{
						// Verify we're still occupying this POI through the handler.
						if (poiHandler.IsOccupying && poiHandler.CurrentPOI == targetPOI)
						{
							// All good, resume dwelling with remaining time.
						}
						else
						{
							// POI occupation was lost. Fall back to idle.
							targetPOI = null;
							activity = WanderActivity.Idle;
							queryTimer = 0f;
						}
					}
					// Dwelling without a POI (regular dwell at a wander point) is valid.
					break;

				case WanderActivity.Moving:
				case WanderActivity.MovingRaw:
					// Resume moving toward destination. If POI was resolved, we'll
					// attempt occupation on arrival. If it wasn't (destroyed/occupied),
					// we still move to the destination and dwell there instead.
					if (targetPOI == null && !string.IsNullOrEmpty(poiId))
					{
						// Had a POI target but it's gone. Navigate to destination anyway
						// and treat as a regular wander point.
					}
					break;

				case WanderActivity.Idle:
					// Nothing special to validate.
					break;
			}

			return true;
		}

		#endregion
	}
}
