using System.Collections;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	[NodeTint("#FD383D"), NodeWidth(130)]
	public class ExitStateMachineNode : StateComponentNodeBase
	{
		public override string UserFacingName => "Exit";

		private CallbackService callbackService;
		private Flow flow;

		public void InjectDependencies(CallbackService callbackService, Flow flow)
		{
			this.callbackService = callbackService;
			this.flow = flow;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			callbackService.StartCoroutine(Stop());
		}

		private IEnumerator Stop()
		{
			// Allow all other components to at least start before exiting.
			yield return null;
			flow.StopFlow();
		}
	}
}
