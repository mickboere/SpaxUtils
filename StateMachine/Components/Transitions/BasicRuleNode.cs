using UnityEngine;

namespace SpaxUtils.StateMachines
{
	public abstract class BasicRuleNode : RuleNodeBase
	{
		[SerializeField, NodeInput] protected Connections.Rule inConnection;
	}
}
