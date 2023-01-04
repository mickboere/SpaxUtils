using System;

namespace SpaxUtils
{
	/// <summary>
	/// Basic <see cref="IEquipmentSlot"/> implementation with no unique inherent qualities.
	/// </summary>
	public class EquipmentSlot : IEquipmentSlot
	{
		/// <inheritdoc/>
		public virtual string ID { get; private set; }

		/// <inheritdoc/>
		public virtual string Type { get; private set; }

		private Action<RuntimeEquipedData> onEquip;
		private Action<RuntimeEquipedData> onUnequip;

		public EquipmentSlot(string uid, string type,
			Action<RuntimeEquipedData> onEquip = null, Action<RuntimeEquipedData> onUnequip = null)
		{
			ID = uid;
			Type = type;
			this.onEquip = onEquip;
			this.onUnequip = onUnequip;
		}

		/// <inheritdoc/>
		public virtual void Equip(RuntimeEquipedData equipedData)
		{
			onEquip?.Invoke(equipedData);
		}

		/// <inheritdoc/>
		public virtual void Unequip(RuntimeEquipedData equipedData)
		{
			onUnequip?.Invoke(equipedData);
		}
	}
}
