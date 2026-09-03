using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Everything one arm is carrying and which of it is in hand. Handed whole to
	/// <see cref="AgentArmsComponent.ArmChangedEvent"/> so UI can render an arm without querying back.
	/// </summary>
	public class ArmState
	{
		public bool IsLeft { get; }
		public ArmSide Side { get; }
		public string IKChain { get; }

		/// <summary>This arm's equipment slots, in cycle order.</summary>
		public IReadOnlyList<IEquipmentSlot> Slots => slots;

		/// <summary>What sits in each slot. Index-aligned with <see cref="Slots"/>; null means empty.</summary>
		public IReadOnlyList<RuntimeEquipedData> Armaments => armaments;

		/// <summary>Which slot this arm reaches for. -1 means unarmed.</summary>
		public int ActiveIndex { get; internal set; } = -1;

		/// <summary>The slot to return to when re-arming after going unarmed.</summary>
		public int LastActiveIndex { get; internal set; } = -1;

		/// <summary>The armament this arm wants in hand, sheathing aside.</summary>
		public RuntimeEquipedData Active =>
			ActiveIndex >= 0 && ActiveIndex < armaments.Count ? armaments[ActiveIndex] : null;

		/// <summary>The armament actually in hand right now, if any.</summary>
		public RuntimeEquipedData Wielded { get; internal set; }

		private readonly List<IEquipmentSlot> slots = new List<IEquipmentSlot>();
		private readonly List<RuntimeEquipedData> armaments = new List<RuntimeEquipedData>();

		public ArmState(bool isLeft)
		{
			IsLeft = isLeft;
			Side = isLeft ? ArmSide.Left : ArmSide.Right;
			IKChain = isLeft ? IKChainConstants.LEFT_ARM : IKChainConstants.RIGHT_ARM;
		}

		internal void AddSlot(IEquipmentSlot slot)
		{
			slots.Add(slot);
			armaments.Add(null);
		}

		internal void SetArmament(int index, RuntimeEquipedData data)
		{
			if (index >= 0 && index < armaments.Count)
			{
				armaments[index] = data;
			}
		}
	}
}
