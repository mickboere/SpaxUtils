using UnityEngine;

namespace SpaxUtils.StateMachines
{
	public abstract class BasicRuleNode : RuleNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule inConnection;
	}
}
