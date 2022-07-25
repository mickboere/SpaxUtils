using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	public static class ModUtil
	{
		/// <summary>
		/// Modifies <paramref name="input"/> using the given list of <paramref name="modifiers"/>, in ascending order of <see cref="ModMethod"/>.
		/// </summary>
		/// <typeparam name="T">The modifier generic.</typeparam>
		/// <param name="input">The value that will be modified to produce the output.</param>
		/// <param name="modifiers">The list of modifiers to be applied to the input.</param>
		public static T Modify<T>(T input, IEnumerable<IModifier<T>> modifiers)
		{
			List<IModifier<T>> sortedModifiers = modifiers.OrderBy((mod) => mod.Method).ToList();

			T baseValue = input;
			T output = input;
			foreach (IModifier<T> modifier in sortedModifiers)
			{
				if (modifier.Method == ModMethod.Base)
				{
					baseValue = ApplyMod(input, baseValue, modifier);
					output = baseValue;
				}
				else
				{
					output = ApplyMod(baseValue, output, modifier);
				}
			}

			return output;
		}

		/// <summary>
		/// Looks at the <paramref name="modifier"/>'s <see cref="ModMethod"/> and applies the mod accordingly.
		/// </summary>
		/// <typeparam name="T">The composite data type to be modded.</typeparam>
		/// <param name="baseValue">The base value of the composite data, required for additive mod application.</param>
		/// <param name="currentValue">The current value of the composite data.</param>
		/// <param name="modifier">The <see cref="IModifier{T}"/> implementation to apply to the composite data.</param>
		/// <returns>The new composite data value after modification.</returns>
		public static T ApplyMod<T>(T baseValue, T currentValue, IModifier<T> modifier)
		{
			switch (modifier.Method)
			{
				case ModMethod.Apply:
				case ModMethod.Absolute:
					return modifier.Modify(currentValue);
				case ModMethod.Auto:
				case ModMethod.Base:
				case ModMethod.Additive:
					return ModAdditive(baseValue, currentValue, modifier);
				default:
					SpaxDebug.Error($"IModifier<{typeof(T)}> ", $"ModMethod: {modifier.Method} could not be applied.");
					return currentValue;
			}
		}

		/// <summary>
		/// Returns an additive modification of <paramref name="baseValue"/>.
		/// <para>(<paramref name="currentValue"/> + <paramref name="modifier"/>(<paramref name="baseValue"/>) - <paramref name="baseValue"/>)</para>
		/// </summary>
		/// <typeparam name="T">The composite data type to be modded.</typeparam>
		/// <param name="baseValue">The base value of the composite data, required for additive mod application.</param>
		/// <param name="currentValue">The current value of the composite data.</param>
		/// <param name="modifier">The <see cref="IModifier{T}"/> implementation to apply additively to the composite data.</param>
		public static T ModAdditive<T>(T baseValue, T currentValue, IModifier<T> modifier)
		{
			// current + mod(base) - base
			return modifier.Add(currentValue, modifier.Subtract(modifier.Modify(baseValue), baseValue));
		}
	}
}
