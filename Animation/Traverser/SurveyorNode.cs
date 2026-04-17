using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Node in control of the <see cref="SurveyorComponent"/>.
	/// </summary>
	public class SurveyorNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private SurveyorComponent surveyorComponent;
		private CallbackService callbackService;

		public void InjectDependencies(SurveyorComponent surveyorComponent, CallbackService callbackService)
		{
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
			surveyorComponent.UpdateSurveyor(delta);
		}
	}
}
