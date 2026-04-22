using UnityEngine;
using SpaxUtils.StateMachines;
using System.Linq;

namespace SpaxUtils
{
	[NodeWidth(300)]
	public class BrainStateRuleNode : RuleNodeBase
	{
		public override bool Valid
		{
			get
			{
				if (any)
				{
					return states.Any((s) => agent.Brain.IsStateActive(s)) != invert;
				}
				else
				{
					return states.All((s) => agent.Brain.IsStateActive(s)) != invert;
				}
			}
		}

		[SerializeField, NodeInput] protected Connections.Rule inConnection;
		[SerializeField, ConstDropdown(typeof(IStateIdentifiers))] private string[] states;
		[SerializeField] private bool any;
		[SerializeField] private bool invert;

		private IAgent agent;

		public void InjectDependencies(IAgent agent)
		{
			this.agent = agent;
		}
	}
}
