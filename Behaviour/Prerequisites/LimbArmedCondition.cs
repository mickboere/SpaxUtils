using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IConditional"/> that checks whether a hand slot is armed or unarmed.
	/// Use case: prevent a follow-up from being available when a specific hand has a weapon equipped.
	/// </summary>
	[Serializable]
	public class LimbArmedCondition : IConditional
	{
		[SerializeField] private bool leftHand = true;
		[SerializeField] private bool requireArmed = true;

		public bool IsMet(IDependencyManager dependencies)
		{
			if (!dependencies.TryGet<EquipmentComponent>(out EquipmentComponent equipment)) return true;
			string slotId = leftHand ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND;
			return equipment.GetEquipedFromSlotID(slotId) != null == requireArmed;
		}
	}
}
