using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class ItemContainer : InteractableComponentBase
	{
		public override string InteractableType => InteractionTypes.CONTAINER;

		[SerializeField] private List<LootTable> loot;

		public override List<string> GetInteractions(IEntity interactor)
		{
			return new List<string>() { InteractionTypes.CONTAINER_OPEN };
		}

		public override bool TryInteract(IInteraction interaction)
		{
			if (interaction.Action == InteractionTypes.CONTAINER_OPEN)
			{
				// For animation: subscribe to interaction.initiation and invoke method with animation.
				// UI menu must be handled on Interactor end.
				return true;
			}

			return false;
		}
	}
}
