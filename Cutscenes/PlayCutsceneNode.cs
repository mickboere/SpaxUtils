using SpaxUtils;
using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Level-flow node that triggers a cutscene via <see cref="CutsceneService"/> and becomes
	/// valid once the cutscene has completed. Use the outgoing rule connection to gate
	/// downstream flow on cutscene completion.
	/// </summary>
	public class PlayCutsceneNode : StateMachineNodeBase, IRule
	{
		public bool Valid { get; private set; } = false;
		public float Validity => Valid ? 1f : 0f;
		public virtual bool IsPureRule => false;

		[SerializeField, NodeInput] protected Connections.StateComponent inConnection;
		[SerializeField, ConstDropdown(typeof(ICutsceneConstants))] private string cutsceneKey;
		[SerializeField, NodeInput] protected Connections.Rule completedRule;

		private CutsceneService cutsceneService;

		public void InjectDependencies(CutsceneService cutsceneService)
		{
			this.cutsceneService = cutsceneService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			Valid = false;
			cutsceneService.Play(cutsceneKey, OnCutsceneComplete);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			Valid = false;
		}

		private void OnCutsceneComplete()
		{
			Valid = true;
		}
	}
}
