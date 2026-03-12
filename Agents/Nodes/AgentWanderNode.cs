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
	/// </summary>
	[NodeWidth(280)]
	public class AgentWanderNode : StateComponentNodeBase
	{
		// Large sample range so NavMesh.SamplePosition succeeds across big flat regions.
		private const float NAVMESH_SAMPLE_RANGE = 25f;

		// Maximum weighted selection attempts before giving up and reverting to Idle.
		private const int MAX_SELECTION_ATTEMPTS = 5;

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
		[SerializeField, Tooltip("Only visit POIs with all of these tags. Leave empty to visit any.")]
		private string[] poiTags;

		[SerializeField, Range(0f, 1f), Tooltip("How strongly proximity biases POI selection. " +
			"0 = uniform, 1 = strongly prefer nearby. Kept soft by default to avoid always picking the same two nearby POIs.")]
		private float poiDistanceFalloff = 0.33f;

		private IAgent agent;
		private AgentNavigationHandler navigation;
		private CallbackService callbackService;
		private ISpawnpoint spawnpoint;
		private WorldRegionService worldRegionService;

		private enum WanderActivity { Idle, Moving, MovingRaw, Dwelling }
		private enum ActivityOption { Region = 0, Radius = 1, POI = 2 }

		private WanderActivity activity;
		private Vector3 currentDestination;
		private PointOfInterest currentPOI;
		private float queryTimer;
		private float dwellTimer;

		// Reused per query to avoid allocation.
		private readonly List<float> activityWeights = new List<float>(3) { 0f, 0f, 0f };

		public void InjectDependencies(
			IAgent agent,
			AgentNavigationHandler navigation,
			CallbackService callbackService,
			WorldRegionService worldRegionService,
			[Optional] ISpawnpoint spawnpoint)
		{
			this.agent = agent;
			this.navigation = navigation;
			this.callbackService = callbackService;
			this.worldRegionService = worldRegionService;
			this.spawnpoint = spawnpoint;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			activity = WanderActivity.Idle;
			currentPOI = null;
			queryTimer = queryInterval;
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
			VacatePOI();
			navigation.ResetInput();
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
						float dwell = currentPOI != null
							? currentPOI.SampleDwellTime()
							: Random.Range(dwellTimeMin, dwellTimeMax);
						BeginDwell(dwell);
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
			List<PointOfInterest> available = spawnpoint.Region.GetAvailablePOIs(poiTags);

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

			if (!chosen.TryOccupy(agent))
			{
				return false;
			}

			currentPOI = chosen;
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

					currentPOI = null;
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

				currentPOI = null;

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
			if (currentPOI != null)
			{
				currentPOI.Vacate(agent);
				currentPOI = null;
			}
		}
	}
}
