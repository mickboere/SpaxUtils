using System;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IModifier{T}"/> float implementation that supports basic modification using a <see cref="SpaxUtils.Operation"/>.
	/// </summary>
	public abstract class FloatModifierBase : IModifier<float>
	{
		public virtual bool Dirty { get; protected set; } = true;

		/// <inheritdoc/>
		public abstract ModMethod Method { get; }

		/// <inheritdoc/>
		public abstract float Modify(float input);

		/// <inheritdoc/>
		public float Add(float a, float b)
		{
			return a + b;
		}

		/// <inheritdoc/>
		public float Subtract(float a, float b)
		{
			return a - b;
		}

		/// <inheritdoc/>
		public virtual void Applied()
		{
			Dirty = false;
		}
	}
}
