using System;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IModifier{T}"/> float implementation that supports basic modification using a <see cref="SpaxUtils.Operation"/>.
	/// </summary>
	public abstract class FloatModifierBase : IModifier<float>
	{
		/// <inheritdoc/>
		public event Action RecalculateEvent;

		/// <inheritdoc/>
		public event Action<IModifier<float>> DisposeEvent;

		/// <inheritdoc/>
		public virtual bool AlwaysRecalculate { get; }

		/// <inheritdoc/>
		public abstract ModMethod Method { get; }

		public virtual void Dispose()
		{
			DisposeEvent?.Invoke(this);
		}

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

		protected void Recalculate()
		{
			RecalculateEvent?.Invoke();
		}
	}
}
