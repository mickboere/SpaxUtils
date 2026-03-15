using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Brain state node that lives in the Passive state.
	/// Watches <see cref="POIHandler"/> for occupied POIs with action identifiers
	/// and dispatches corresponding <see cref="ActSignal"/>s to the <see cref="IActor"/>.
	/// On state exit (e.g. entering Combat), cancels any active animated action and vacates the POI.
	/// </summary>
	public class AnimatedActionsNode : StateComponentNodeBase
	{
		private IActor actor;
		private POIHandler poiHandler;
		private AnimationPerformerComponent animationPerformer;
		private CallbackService callbackService;

		/// <summary>
		/// The action identifier currently being performed, or null if none.
		/// </summary>
		private string activeActionId;

		public void InjectDependencies(IActor actor, POIHandler poiHandler,
			AnimationPerformerComponent animationPerformer, CallbackService callbackService)
		{
			this.actor = actor;
			this.poiHandler = poiHandler;
			this.animationPerformer = animationPerformer;
			this.callbackService = callbackService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			activeActionId = null;
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			callbackService.UnsubscribeUpdates(this);

			// Cancel any running animated action.
			CancelActiveAction(true);

			// Vacate POI when leaving Passive (e.g. entering Combat).
			poiHandler.Vacate();
		}

		private void OnUpdate(float delta)
		{
			string poiActionId = poiHandler.CurrentActionIdentifier;

			if (poiActionId != activeActionId)
			{
				// POI changed or was cleared. Cancel current action if any.
				if (activeActionId != null)
				{
					CancelActiveAction(false);
				}

				// Start new action if the new POI has one and the performer supports it.
				if (!string.IsNullOrEmpty(poiActionId) && animationPerformer.SupportsAct(poiActionId))
				{
					actor.Send(new ActSignal(poiActionId, interuptable: true, interuptor: false));
					activeActionId = poiActionId;
				}
			}

			// If the performer completed naturally (non-prolonged action), clear our tracking.
			if (activeActionId != null && animationPerformer.CurrentAction == null)
			{
				activeActionId = null;
			}
		}

		private void CancelActiveAction(bool force)
		{
			if (activeActionId != null)
			{
				// Cancel directly on the performer rather than through Actor,
				// to avoid accidentally cancelling a combat performance.
				animationPerformer.TryCancel(force);
				activeActionId = null;
			}
		}
	}
}
