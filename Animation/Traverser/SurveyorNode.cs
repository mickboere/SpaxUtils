using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Node in control of the <see cref="SurveyorComponent"/>.
	/// </summary>
	public class SurveyorNode : StateMachineNodeBase
	{
		protected Poser Poser => agentPoser.GetMainPoser(PoserLayerConstants.BODY);

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private AnimatorPoser agentPoser;
		private SurveyorComponent surveyorComponent;
		private CallbackService callbackService;

		public void InjectDependencies(AnimatorPoser agentPoser,
			SurveyorComponent surveyorComponent, CallbackService callbackService)
		{
			this.agentPoser = agentPoser;
			this.surveyorComponent = surveyorComponent;
			this.callbackService = callbackService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			callbackService.SubscribeUpdate(UpdateMode.FixedUpdate, this, OnUpdate);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			callbackService.UnsubscribeUpdates(this);
			surveyorComponent.ResetSurveyor();
		}

		private void OnUpdate(float delta)
		{
			if (Poser.GetDominantInstructions().Transition.TryEvaluateFloat(AnimationFloatConstants.FEET_SPACING, 0f, out float spacing))
			{
				surveyorComponent.Spacing = spacing;
			}
			else
			{
				surveyorComponent.Spacing = surveyorComponent.DefaultSpacing;
			}

			surveyorComponent.UpdateSurveyor(delta);
		}
	}
}
