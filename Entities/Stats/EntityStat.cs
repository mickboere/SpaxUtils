using System;
using System.Collections.Generic;
using UnityEngine;

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
				if (!data.Value.Equals(value))
				{
					data.Value = value;
					// No need to call ValueChanged() here as that will happen automatically after the data's ValueChangedEvent.
				}
			}
		}

		private RuntimeDataEntry data;
		private float? minValue;
		private float? maxValue;

		public EntityStat(RuntimeDataEntry data,
			Dictionary<object, IModifier<float>> modifiers = null,
			float? minValue = null, float? maxValue = null) : base(modifiers)
		{
			this.data = data;
			this.minValue = minValue;
			this.maxValue = maxValue;

			data.ValueChangedEvent += OnDataValueChanged;
		}

		public override float GetValue()
		{
			float value = base.GetValue();
			if (minValue.HasValue && value < minValue.Value)
			{
				value = minValue.Value;
			}
			if (maxValue.HasValue && value > maxValue.Value)
			{
				value = maxValue.Value;
			}
			return value;
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
