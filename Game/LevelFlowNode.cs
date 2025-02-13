using SpaxUtils.StateMachines;
using System.Collections;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Node that runs a another flow state machine.
	/// </summary>
	[NodeTint("#6d88a6"), NodeWidth(200)]
	public class LevelFlowNode : StateMachineNodeBase, IRule
	{
		public bool Valid => !subFlow.Running;
		public float Validity => 1f;
		public virtual bool IsPureRule => false;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule exitedFlowRule;

		private IDependencyManager dependencyManager;
		private IHistory history;
		CallbackService callbackService;
		private GameData gameData;
		private SceneService sceneService;

		private FlowGraph flowGraph;
		private Flow subFlow;
		private Coroutine coroutine;

		public void InjectDependencies(IDependencyManager dependencyManager, IHistory history, CallbackService callbackService, GameData gameData, SceneService sceneService)
		{
			this.dependencyManager = dependencyManager;
			this.history = history;
			this.callbackService = callbackService;
			this.gameData = gameData;
			this.sceneService = sceneService;
		}

		protected void OnValidate()
		{
			Init();
		}

		public override void OnEnteringState(ITransition transition)
		{
			base.OnEnteringState(transition);

			if (gameData.Levels.ContainsKey(sceneService.CurrentScene))
			{
				flowGraph = gameData.Levels[sceneService.CurrentScene].FlowGraph;
				coroutine = callbackService.StartCoroutine(DelayedStart(transition));
			}
			else
			{
				SpaxDebug.Error("No level flow graph defined for currently opened scene:", sceneService.CurrentScene);
			}
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