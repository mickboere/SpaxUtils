using System;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IModifier{T}"/> float implementation that supports any type of modification through a <see cref="Func{T, TResult}"/>.
	/// Note that Func modifiers can NOT be saved.
	/// </summary>
	public class FloatFuncModifier : FloatModifierBase
	{
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

		/// <summary>
		/// Should be called whenever the func has changed, causing a difference in output.
		/// </summary>
		public void FuncChanged()
		{
			OnModChanged();
		}
	}
}
