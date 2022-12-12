using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="CompositeFloatBase"/> implementation that allows modifications to an entity's <see cref="RuntimeDataEntry"/>s.
	/// </summary>
	public class EntityStat : CompositeFloatBase, IDisposable
	{
		/// <summary>
		/// Links to the identifier of the attached <see cref="RuntimeDataEntry"/>.
		/// </summary>
		public string Identifier => data.ID;

		public override float BaseValue
		{
			get
			{
				return (float)data.Value;
			}
			set
			{
				data.Value = value;
				// No need to call ValueChanged() here as that will happen automatically after the data's ValueChangedEvent.
			}
		}

		private RuntimeDataEntry data;

		public EntityStat(RuntimeDataEntry data, Dictionary<object, IModifier<float>> modifiers = null) : base(modifiers)
		{
			this.data = data;
			data.ValueChangedEvent += OnDataValueChanged;
		}

		public void Dispose()
		{
			data.ValueChangedEvent -= OnDataValueChanged;
		}

		private void OnDataValueChanged(object newValue)
		{
			ValueChanged();
		}
	}
}
