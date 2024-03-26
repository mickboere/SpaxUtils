using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// <see cref="IStateTransition"/> that utilizes <see cref="IRule"/> nodes to determine whether the transition is valid.
	/// Requires all connected <see cref="RuleNodeBase"/> implementations to be valid in order to pass as valid itself.
	/// </summary>
	[NodeWidth(140)]
	public abstract class RuledTransitionNodeBase : TransitionNodeBase
	{
		public override bool Valid => IsValid();
		public override float Validity => ruleNodes.Sum((r) => r.Validity);

		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] protected Connections.Rule rules;
		protected List<IRule> ruleNodes;

		public override void OnPrepare()
		{
			base.OnPrepare();
			ruleNodes = GetOutputNodes<IRule>(nameof(rules));
		}

		private bool IsValid()
		{
			foreach (IRule rule in ruleNodes)
			{
				if (!rule.Valid)
				{
					return false;
				}
			}
			return true;
		}
	}
}