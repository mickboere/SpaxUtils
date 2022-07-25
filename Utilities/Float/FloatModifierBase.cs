using System;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IModifier{T}"/> float implementation that supports basic modification using a <see cref="SpaxUtils.Operation"/>.
	/// </summary>
	public abstract class FloatModifierBase : IModifier<float>, IDisposable
	{
		public event Action ModChangedEvent;

		/// <inheritdoc/>
		public abstract ModMethod Method { get; }

		public virtual void Dispose()
		{
			OnModChanged();
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

		/// <summary>
		/// Will invoke the <see cref="ModChangedEvent"/>.
		/// </summary>
		protected void OnModChanged()
		{
			ModChangedEvent?.Invoke();
		}
	}
}
