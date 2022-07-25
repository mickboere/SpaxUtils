using System;

namespace SpaxUtils
{
	/// <summary>
	/// Generic interface for a composite value modifier.
	/// </summary>
	/// <typeparam name="T">The type of value this modifier can mod.</typeparam>
	public interface IModifier<T>
	{
		/// <summary>
		/// Invoked when any modifier property has changed.
		/// Used to minimize the calculations required to get an accurate final value.
		/// </summary>
		event Action ModChangedEvent;

		/// <summary>
		/// Defines the type of modification.
		/// </summary>
		ModMethod Method { get; }

		/// <summary>
		/// Modifies the given input using the properties defined in the modifier implementation.
		/// </summary>
		T Modify(T input);

		/// <summary>
		/// (a+b): Adds generic <paramref name="a"/> to generic <paramref name="b"/>.
		/// </summary>
		T Add(T a, T b);

		/// <summary>
		/// (a-b): Subtracts generic <paramref name="b"/> from generic <paramref name="a"/>.
		/// </summary>
		T Subtract(T a, T b);
	}
}
