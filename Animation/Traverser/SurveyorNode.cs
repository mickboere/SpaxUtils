using SpaxUtils.StateMachine;
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
		[SerializeField] private PoseSequenceBlendTree movementBlendTree;

		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser agentPoser;
		private IAgentMovementHandler agentMovementHandler;
		private SurveyorComponent surveyorComponent;
		private CallbackService callbackService;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, AnimatorPoser agentPoser,
			IAgentMovementHandler agentMovementHandler, SurveyorComponent surveyorComponent,
			CallbackService callbackService)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.agentPoser = agentPoser;
			this.agentMovementHandler = agentMovementHandler;
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
