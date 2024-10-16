﻿using System;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="FloatModifierBase"/> implementation that supports any type of modification through a <see cref="Func{float, float}"/>.
	/// <see cref="FuncChanged"/> should be invoked anytime the func values change.
	/// </summary>
	public class FloatFuncModifier : FloatModifierBase
	{
		public override bool AlwaysRecalculate => true;
		public override ModMethod Method => method;
		public Func<float, float> Func { get; protected set; }

		private ModMethod method;

		public FloatFuncModifier(ModMethod method, Func<float, float> func)
		{
			this.method = method;
			Func = func;
		}

		public override float Modify(float input)
		{
			return Func(input);
		}
	}
}
