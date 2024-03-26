using SpaxUtils.StateMachines;
using System.Collections;
using System.Collections.Generic;
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
			callbackService.FixedUpdateCallback += OnFixedUpdateCallback;
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			callbackService.FixedUpdateCallback -= OnFixedUpdateCallback;
			surveyorComponent.ResetSurveyor();
		}

		private void OnFixedUpdateCallback()
		{
			surveyorComponent.UpdateSurveyor(Time.fixedDeltaTime);
		}
	}
}
