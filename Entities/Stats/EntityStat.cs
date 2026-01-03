using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="CompositeFloatBase"/> implementation that allows modifications to an entity's <see cref="RuntimeDataEntry"/>s.
	/// </summary>
	public class EntityStat : CompositeFloatBase
	{
		/// <summary>
		/// Links to the identifier of the attached <see cref="RuntimeDataEntry"/>.
		/// </summary>
		public string Identifier => Data.ID;

		public RuntimeDataEntry Data { get; private set; }

		public override float BaseValue
		{
			get
			{
				return (float)Data.Value;
			}
			set
			{
				float clamp = ClampValue(value);
				if (!Data.Value.Equals(clamp))
				{
					Data.Value = clamp;
					// No need to call ValueChanged() here as that will happen automatically after the data's ValueChangedEvent.
				}
			}
		}

		public override float ModdedBaseValue => ClampValue(base.ModdedBaseValue);

		private IEntity entity;
		private float? minValue;
		private float? maxValue;
		private DecimalMethod decimals;

		private EntityStat drainMultiplier;
		private EntityStat gainMultiplier;

		public EntityStat(IEntity entity, RuntimeDataEntry data,
			Dictionary<object, IModifier<float>> modifiers = null,
			float? minValue = null, float? maxValue = null,
			DecimalMethod decimals = DecimalMethod.Decimal) : base(modifiers)
		{
			this.entity = entity;
			this.Data = data;

			this.minValue = minValue;
			this.maxValue = maxValue;
			this.decimals = decimals;

			data.ValueChangedEvent += OnDataValueChanged;
		}

		public override float GetValue()
		{
			return ClampValue(base.GetValue()).Decimal(decimals);
		}

		public override void Dispose()
		{
			Data.ValueChangedEvent -= OnDataValueChanged;
			base.Dispose();
		}

		private float ClampValue(float value)
		{
			if (minValue.HasValue && value < minValue.Value)
			{
				value = minValue.Value;
			}
			else if (maxValue.HasValue && value > maxValue.Value)
			{
				value = maxValue.Value;
			}
			return value;
		}

		private void OnDataValueChanged(object newValue)
		{
			ValueChanged();
		}
	}
}
