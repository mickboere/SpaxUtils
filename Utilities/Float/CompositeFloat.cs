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

		public CompositeFloat(float baseValue, Dictionary<object, IModifier<float>> modifiers = null) : base(modifiers)
		{
			this.baseValue = baseValue;
		}

		public CompositeFloat Clone()
		{
			return new CompositeFloat(baseValue, new Dictionary<object, IModifier<float>>(modifiers));
		}
	}
}
