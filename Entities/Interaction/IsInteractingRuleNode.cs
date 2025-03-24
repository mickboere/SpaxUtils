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

		private InteractionHandler interactionHandler;

		public void InjectDependencies(InteractionHandler interactionHandler)
		{
			this.interactionHandler = interactionHandler;
		}
	}
}
