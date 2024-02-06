using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Default implementation for <see cref="CompositeFloatBase"/>, taking any float as base modifyable value.
	/// </summary>
	[Serializable]
	public class CompositeFloat : CompositeFloatBase
	{
		public override float BaseValue
		{
			get
			{
				return baseValue;
			}
			set
			{
				baseValue = value;
				ValueChanged();
			}
		}

		[SerializeField] protected float baseValue;

		private float? minValue;
		private float? maxValue;

		public CompositeFloat(float baseValue,
			Dictionary<object, IModifier<float>> modifiers = null,
			float? minValue = null, float? maxValue = null) : base(modifiers)
		{
			this.baseValue = baseValue;
			this.minValue = minValue;
			this.maxValue = maxValue;
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

		public CompositeFloat Clone()
		{
			return new CompositeFloat(baseValue, new Dictionary<object, IModifier<float>>(modifiers));
		}
	}
}
