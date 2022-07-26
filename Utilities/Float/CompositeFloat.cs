﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class CompositeFloat : CompositeFloatBase
	{
		public override float BaseValue
		{
			get
			{
				// return ModUtil.Modify(baseValue, modifiers.Values.Where(m => m.Method == ModMethod.Base).ToList());
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
