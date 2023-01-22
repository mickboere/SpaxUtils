using SpaxUtils.StateMachine;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Turns valid when the <see cref="ITargeter"/> is currently targetting its target.
	/// </summary>
	[NodeWidth(200)]
	public class AgentIsTargetingRuleNode : RuleNodeBase
	{
		public override bool Valid => targeter.Target != null != invert;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule inConnection;
		[SerializeField] private bool invert;

		private ITargeter targeter;

		public void InjectDependencies(ITargeter targeter)
		{
			this.targeter = targeter;
		}
	}
}
