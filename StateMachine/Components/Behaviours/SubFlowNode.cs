using System.Collections;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Node that runs a another flow state machine.
	/// </summary>
	[NodeTint("#6d88a6"), NodeWidth(200)]
	public class SubFlowNode : StateMachineNodeBase, IRule
	{
		public override string UserFacingName => flowGraph != null ? flowGraph.name : "Empty";

		public bool Valid => !subFlow.Running;
		public float Validity => 1f;
		public virtual bool IsPureRule => false;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule exitedFlowRule;
		[SerializeField] private FlowGraph flowGraph;

		private IDependencyManager dependencyManager;
		private IHistory history;
		CallbackService callbackService;

		private Flow subFlow;
		private Coroutine coroutine;

		public void InjectDependencies(IDependencyManager dependencyManager, IHistory history, CallbackService callbackService)
		{
			this.dependencyManager = dependencyManager;
			this.history = history;
			this.callbackService = callbackService;
		}

		protected void OnValidate()
		{
			Init();
		}

		public override void OnEnteringState(ITransition transition)
		{
			base.OnEnteringState(transition);
			SpaxDebug.Log($"OnEnteringState({transition})");
			coroutine = callbackService.StartCoroutine(DelayedStart(transition));
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			if (coroutine != null)
			{
				callbackService.StopCoroutine(coroutine);
				coroutine = null;
			}
			if (subFlow != null)
			{
				subFlow.Dispose();
				subFlow = null;
			}
		}

		private IEnumerator DelayedStart(ITransition transition)
		{
			// Delay start to prevent frame-0 order bugs.
			yield return null;
			subFlow = new Flow(flowGraph, dependencyManager, history);
			subFlow.StartFlow(transition);
			coroutine = null;
		}
	}
}