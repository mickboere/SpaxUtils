using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class IsInteractingRuleNode : RuleNodeBase
	{
		public override bool Valid => (interactionHandler != null && interactionHandler.Interactions.Count > 0) != invert;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule inConnection;
		[SerializeField] private bool invert;

		private IInteractionHandler interactionHandler;

		public void InjectDependencies(IInteractionHandler interactionHandler)
		{
			this.interactionHandler = interactionHandler;
		}
	}
}
