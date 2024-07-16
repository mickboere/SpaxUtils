using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Calculates a float value by applying a collection of modifiers to a base value.
	/// Calculations happen on-demand to prevent recalculations for every change.
	/// </summary>
	public abstract class CompositeFloatBase : IDisposable
	{
		/// <summary>
		/// Called whenever any value within the composite has been changed.
		/// </summary>
		public event Action ValueChangedEvent;

		/// <summary>
		/// Called whenever any value within the composite has been changed, returns this <see cref="CompositeFloat"/>.
		/// </summary>
		public event Action<CompositeFloatBase> CompositeChangedEvent;

		/// <summary>
		/// Calculated modified value.
		/// </summary>
		public float Value { get { return GetValue(); } }

		/// <summary>
		/// Returns the base value without modifications.
		/// </summary>
		public abstract float BaseValue { get; set; }

		/// <summary>
		/// Returns the <see cref="BaseValue"/> but with all "Base" modifiers applied.
		/// </summary>
		public float ModdedBaseValue => ModUtil.Modify(BaseValue, modifiers.Values.Where(m => m.Method == ModMethod.Base).ToList());

		/// <summary>
		/// Returns true if there are any modifiers.
		/// </summary>
		public bool HasModifiers { get { return modifiers.Count > 0; } }

		/// <summary>
		/// Returns the difference between the modified value and the base value.
		/// </summary>
		public float Difference => Value - BaseValue;

		/// <summary>
		/// Returns the percentage of the value in relation to the base value.
		/// </summary>
		public float NormalizedDifference => Value / BaseValue;

		/// <summary>
		/// Returns a readonly collection of all modifiers within this composite.
		/// </summary>
		public IReadOnlyDictionary<object, IModifier<float>> Modifiers => modifiers;

		protected Dictionary<object, IModifier<float>> modifiers;

		private bool recalculate;
		private float lastCalculatedValue;

		public CompositeFloatBase(Dictionary<object, IModifier<float>> modifiers = null)
		{
			this.modifiers = modifiers;

			if (this.modifiers == null)
			{
				this.modifiers = new Dictionary<object, IModifier<float>>();
			}

			recalculate = true;
		}

		/// <summary>
		/// Disposes of this composite, cleaning up all its data.
		/// </summary>
		public virtual void Dispose()
		{
			ClearModifiers();
		}

		/// <summary>
		/// Calculates and returns the composite value.
		/// </summary>
		public virtual float GetValue()
		{
			if (recalculate || modifiers.Values.Any((m) => m.AlwaysRecalculate))
			{
				// Recalculation is required, apply all modifiers to base value. (Mod ordering is handled internally)
				lastCalculatedValue = ModUtil.Modify(BaseValue, modifiers.Values);
				recalculate = false;
			}

			return lastCalculatedValue;
		}

		/// <summary>
		/// Adds a new float modifier to the composite.
		/// </summary>
		/// <param name="modIdentifier">The identifier object of the modifier, used to remove the modifier later.</param>
		/// <param name="modifier">The float modifier to add to the composite.</param>
		public void AddModifier(object modIdentifier, IModifier<float> modifier)
		{
			// If the mod method is Apply, apply the mod to the base value and return.
			if (modifier.Method == ModMethod.Apply)
			{
				BaseValue = modifier.Modify(BaseValue);
				return;
			}

			// If we already have a mod using this identifier, first remove the existing one.
			if (HasModifier(modIdentifier))
			{
				RemoveModifier(modIdentifier);
			}

			// Add the mod and request a recalculation.
			modifiers[modIdentifier] = modifier;
			modifier.RecalculateEvent += OnModifierRecalculateEvent;
			modifier.DisposeEvent += OnModifierDisposeEvent;
			ValueChanged();
		}

		/// <summary>
		/// Removes the float modifier stored with <paramref name="modIdentifier"/>.
		/// </summary>
		/// <param name="modIdentifier">The identifier of the mod to remove.</param>
		public void RemoveModifier(object modIdentifier)
		{
			if (HasModifier(modIdentifier))
			{
				modifiers.Remove(modIdentifier, out IModifier<float> mod);
				mod.RecalculateEvent -= OnModifierRecalculateEvent;
				ValueChanged();
			}
		}

		/// <summary>
		/// Removes the float modifier.
		/// </summary>
		/// <param name="modifier">The modifier to remove from the composite.</param>
		public void RemoveModifier(IModifier<float> modifier)
		{
			foreach (KeyValuePair<object, IModifier<float>> kvp in modifiers)
			{
				if (kvp.Value == modifier)
				{
					RemoveModifier(kvp.Key);
					return;
				}
			}
		}

		/// <summary>
		/// Clears all modifiers from the composite.
		/// </summary>
		public void ClearModifiers()
		{
			foreach (IModifier<float> modifier in modifiers.Values)
			{
				modifier.RecalculateEvent -= OnModifierRecalculateEvent;
				modifier.DisposeEvent -= OnModifierDisposeEvent;
			}
			modifiers.Clear();
			ValueChanged();
		}

		/// <summary>
		/// Returns whether this composite contains a modifier with identifier <paramref name="modIdentifier"/>.
		/// </summary>
		/// <param name="modIdentifier">The identifier of the modifier to check.</param>
		/// <returns>Whether this composite contains a modifier with identifier <paramref name="modIdentifier"/>.</returns>
		public bool HasModifier(object modIdentifier)
		{
			return modifiers.ContainsKey(modIdentifier);
		}

		/// <summary>
		/// Will return the float modifier that was stored with identifier <paramref name="modIdentifier"/>.
		/// </summary>
		/// <param name="modIdentifier"></param>
		/// <returns></returns>
		public IModifier<float> GetModifier(object modIdentifier)
		{
			return modifiers[modIdentifier];
		}

		/// <summary>
		/// Tries to retrieve a modifier with identifier <paramref name="modIdentifier"/>.
		/// </summary>
		/// <param name="modIdentifier">The identifier that was used to store the modifier.</param>
		/// <param name="modifier">The successfully retrieved modifier, if any.</param>
		/// <returns>Whether the modifier has been successfully retrieved.</returns>
		public bool TryGetModifier(object modIdentifier, out IModifier<float> modifier)
		{
			modifier = null;
			if (HasModifier(modIdentifier))
			{
				modifier = GetModifier(modIdentifier);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Called whenever there has been a modification to the composite.
		/// </summary>
		protected void ValueChanged()
		{
			recalculate = true;
			ValueChangedEvent?.Invoke();
			CompositeChangedEvent?.Invoke(this);
		}

		/// <summary>
		/// Invoked after a modifier has been updated.
		/// </summary>
		private void OnModifierRecalculateEvent()
		{
			ValueChanged();
		}

		/// <summary>
		/// Invoked after a modifier has been updated.
		/// </summary>
		private void OnModifierDisposeEvent(IModifier<float> modifier)
		{
			RemoveModifier(modifier);
		}

		/// <summary>
		/// Implicit float cast so that you don't have to call <see cref="GetValue"/> all the time.
		/// </summary>
		public static implicit operator float(CompositeFloatBase composite)
		{
			if (composite == null)
			{
				throw new ArgumentNullException("", "Composite is null and could therefore not be converted to a float.");
			}
			return composite.Value;
		}
	}
}
