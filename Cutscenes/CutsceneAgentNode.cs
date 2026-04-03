using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Brain node that listens to <see cref="CutsceneService"/> events and transitions the agent
	/// into/out of the Cutscene brain state. Should live on a layer above the Cutscene state
	/// (e.g. Active layer) so it remains active during cutscene playback.
	/// Reusable across any agent that needs cutscene-awareness.
	/// </summary>
	public class CutsceneAgentNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		private CutsceneService cutsceneService;
		private IAgent agent;

		public void InjectDependencies(CutsceneService cutsceneService, IAgent agent)
		{
			this.cutsceneService = cutsceneService;
			this.agent = agent;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			cutsceneService.CutsceneStartedEvent += OnCutsceneStarted;
			cutsceneService.CutsceneEndedEvent += OnCutsceneEnded;

			// If a cutscene is already playing when this node enters, transition immediately.
			if (cutsceneService.Playing)
			{
				agent.Brain.TryTransition(AgentStateIdentifiers.CUTSCENE);
			}
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			cutsceneService.CutsceneStartedEvent -= OnCutsceneStarted;
			cutsceneService.CutsceneEndedEvent -= OnCutsceneEnded;
		}

		private void OnCutsceneStarted()
		{
			agent.Brain.TryTransition(AgentStateIdentifiers.CUTSCENE);
		}

		private void OnCutsceneEnded()
		{
			agent.Brain.TryTransition(AgentStateIdentifiers.ACTIVE);
		}
	}
}
