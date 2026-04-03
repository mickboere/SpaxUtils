using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Reusable helper that navigates an agent to a <see cref="PointOfInterest"/> and occupies it.
	/// Can be created at runtime by any system that needs to send an agent to a POI.
	/// Handles navigation subscription internally; the caller only needs to check state.
	/// Does NOT manage occupation lifetime -- vacating is the caller's responsibility via <see cref="POIHandler"/>.
	/// </summary>
	public class POIVisitHelper : IDisposable
	{
		public enum VisitState
		{
			Idle,
			Navigating,
			Arrived,
			Failed
		}

		/// <summary>
		/// Current state of the visit.
		/// </summary>
		public VisitState State { get; private set; }

		/// <summary>
		/// Whether the helper is currently navigating toward a POI.
		/// </summary>
		public bool IsNavigating => State == VisitState.Navigating;

		/// <summary>
		/// Whether the helper has successfully arrived at and occupied the target POI.
		/// </summary>
		public bool HasArrived => State == VisitState.Arrived;

		/// <summary>
		/// Whether the visit failed (e.g. POI not found or already occupied).
		/// </summary>
		public bool HasFailed => State == VisitState.Failed;

		private IAgent agent;
		private AgentNavigationHandler navigation;
		private EntityService entityService;
		private POIHandler poiHandler;
		private CallbackService callbackService;

		private PointOfInterest targetPOI;
		private float moveSpeed;
		private float arrivalRange;
		private bool subscribed;

		public POIVisitHelper(
			IAgent agent,
			AgentNavigationHandler navigation,
			EntityService entityService,
			POIHandler poiHandler,
			CallbackService callbackService)
		{
			this.agent = agent;
			this.navigation = navigation;
			this.entityService = entityService;
			this.poiHandler = poiHandler;
			this.callbackService = callbackService;
			State = VisitState.Idle;
		}

		/// <summary>
		/// Begin visiting a POI resolved by entity ID.
		/// </summary>
		/// <param name="poiEntityId">The entity ID of the POI to visit.</param>
		/// <param name="immediate">If true, teleport and occupy immediately. If false, navigate.</param>
		/// <param name="moveSpeed">Navigation movement speed.</param>
		/// <param name="arrivalRange">Distance at which the agent is considered to have arrived.</param>
		/// <returns>True if the visit was successfully started or completed.</returns>
		public bool Visit(string poiEntityId, bool immediate, float moveSpeed = 0.5f, float arrivalRange = 0.25f)
		{
			if (entityService == null)
			{
				SpaxDebug.Error("POIVisitHelper: EntityService is null, cannot resolve POI by ID.");
				State = VisitState.Failed;
				return false;
			}

			if (!entityService.TryGet(poiEntityId, out IEntity poiEntity)
				|| !poiEntity.TryGetEntityComponent<PointOfInterest>(out PointOfInterest poi))
			{
				SpaxDebug.Error("POIVisitHelper: Could not resolve POI.", poiEntityId);
				State = VisitState.Failed;
				return false;
			}

			return Visit(poi, immediate, moveSpeed, arrivalRange);
		}

		/// <summary>
		/// Begin visiting a specific POI reference.
		/// </summary>
		/// <param name="poi">The POI to visit.</param>
		/// <param name="immediate">If true, teleport and occupy immediately. If false, navigate.</param>
		/// <param name="moveSpeed">Navigation movement speed.</param>
		/// <param name="arrivalRange">Distance at which the agent is considered to have arrived.</param>
		/// <returns>True if the visit was successfully started or completed.</returns>
		public bool Visit(PointOfInterest poi, bool immediate, float moveSpeed = 0.5f, float arrivalRange = 0.25f)
		{
			// Clean up any previous visit.
			Dispose();

			if (poi == null)
			{
				State = VisitState.Failed;
				return false;
			}

			targetPOI = poi;
			this.moveSpeed = moveSpeed;
			this.arrivalRange = arrivalRange;

			if (immediate)
			{
				return TryOccupy();
			}

			State = VisitState.Navigating;
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
			subscribed = true;
			return true;
		}

		/// <summary>
		/// Stops navigation and resets the helper to idle.
		/// Does NOT vacate -- occupation is managed by <see cref="POIHandler"/>.
		/// </summary>
		public void Dispose()
		{
			StopNavigation();
			targetPOI = null;
			State = VisitState.Idle;
		}

		private void OnUpdate(float delta)
		{
			if (State != VisitState.Navigating || targetPOI == null)
			{
				StopNavigation();
				return;
			}

			if (navigation.MoveInRange(arrivalRange, moveSpeed, true, targetPOI.transform.position))
			{
				TryOccupy();
			}
		}

		private bool TryOccupy()
		{
			StopNavigation();

			if (targetPOI != null && targetPOI.TryOccupy(agent))
			{
				poiHandler.Occupy(targetPOI);
				State = VisitState.Arrived;
				return true;
			}

			State = VisitState.Failed;
			return false;
		}

		private void StopNavigation()
		{
			if (subscribed)
			{
				callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
				subscribed = false;
			}
			navigation.ResetInput();
		}
	}
}
