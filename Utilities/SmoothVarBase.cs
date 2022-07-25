using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract base class for a variable that needs to be smoothed out (over time).
	/// Smoothing is done by storing a collection of <typeparamref name="T"/> and calculating a (weighted) average.
	/// </summary>
	/// <typeparam name="T">The type of value this implementation intends to smooth.</typeparam>
	public abstract class SmoothVarBase<T>
	{
		protected int capacity;
		protected bool weightFalloff;
		protected List<T> stack;
		
		public SmoothVarBase(int capacity, bool weightFalloff = true)
		{
			this.capacity = capacity;
			this.weightFalloff = weightFalloff;
			stack = new List<T>();
		}

		public void Push(T value)
		{
			stack.Add(value);
			if (stack.Count == capacity)
			{
				stack.RemoveAt(0);
			}
		}

		public abstract T GetValue();

		/// <summary>
		/// Implicit type cast so that you don't have to call <see cref="GetValue"/> all the time.
		/// </summary>
		public static implicit operator T(SmoothVarBase<T> smoothVar)
		{
			return smoothVar.GetValue();
		}
	}
}
