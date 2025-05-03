using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	[NodeWidth(300)]
	public class MatchesRuntimeDataRuleNode : RuleNodeBase
	{
		public override bool Valid => _valid;
		private bool _valid;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule ruleConnection;
		[SerializeField] private LabeledDataCollection data;

		private RuntimeDataCollection runtimeDataCollection;

		public void InjectDependencies(RuntimeDataCollection runtimeDataCollection)
		{
			this.runtimeDataCollection = runtimeDataCollection;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			_valid = data.Matches(runtimeDataCollection);
		}
	}
}
