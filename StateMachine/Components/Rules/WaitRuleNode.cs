using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Node that waits between a min and a max amount of time before turning valid.
	/// </summary>
	[NodeWidth(150)]
	public class WaitRuleNode : RuleNodeBase
	{
		public override bool Valid => valid;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule inConnection;
		[SerializeField] private float minWait;
		[SerializeField] private float maxWait;

		private bool valid;
		private CallbackService callbackService;

		public override void OnPrepare()
		{
			base.OnPrepare();
			valid = false;
		}

		public void InjectDependencies(CallbackService callbackService)
		{
			this.callbackService = callbackService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			callbackService.StartCoroutine(WaitMethod(Random.Range(minWait, maxWait)));
		}

		private IEnumerator WaitMethod(float time)
		{
			yield return new WaitForSeconds(time);
			valid = true;
		}
	}
}
