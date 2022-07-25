using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Calculates a final value by applying a collection of modifiers to a base value.
	/// Calculations happen on-demand to prevent a recalculation for every change.
	/// </summary>
	public abstract class CompositeFloatBase
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
		/// The base value without any modifiers applied.
		/// </summary>
		public abstract float BaseValue { get; set; }

		/// <summary>
		/// Returns true if there are any modifiers.
		/// </summary>
		public bool HasModifiers { get { return modifiers.Count > 0; } }

		/// <summary>
		/// Returns the difference between the value and the base value.
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
		/// Calculates and returns the composite value.
		/// </summary>
		public float GetValue()
		{
			if (recalculate)
			{
				// Remove all modifiers that have been disposed of.
				//RemoveAllNullMods();

				// Apply modifiers (ordering is handled by util method)
				lastCalculatedValue = ModUtil.Modify(BaseValue, modifiers.Values);

				recalculate = false;
			}

			return lastCalculatedValue;
		}

		public void AddModifier(object modIdentifier, IModifier<float> modifier)
		{
			// If the mod method is apply, apply the mod to the base value and return.
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
			modifier.ModChangedEvent += OnModChangedEvent;
			ValueChanged();
		}

		public void RemoveModifier(object moddIdentifier)
		{
			if (TryGetModifier(moddIdentifier, out IModifier<float> modifier))
			{
				modifiers.Remove(moddIdentifier);
				modifier.ModChangedEvent -= OnModChangedEvent;
				ValueChanged();
			}
			else
			{
				SpaxDebug.Warning("CompositeFloat.RemoveModifier: ", $"No modifier using id [{moddIdentifier}] was found.");
			}
		}

		public void ClearModifiers()
		{
			foreach (IModifier<float> modifier in modifiers.Values)
			{
				modifier.ModChangedEvent -= OnModChangedEvent;
			}
			modifiers.Clear();
			ValueChanged();
		}

		public bool HasModifier(object modIdentifier)
		{
			return modifiers.ContainsKey(modIdentifier);
		}

		public IModifier<float> GetModifier(object modIdentifier)
		{
			return modifiers[modIdentifier];
		}

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
		/// Called whenever a change in mods has been detected.
		/// </summary>
		protected void ValueChanged()
		{
			recalculate = true;
			ValueChangedEvent?.Invoke();
			CompositeChangedEvent?.Invoke(this);
		}

		protected void OnModChangedEvent()
		{
			ValueChanged();
		}

		private void RemoveAllNullMods()
		{
			List<object> nullRefs = modifiers.Where(pair => pair.Value == null).Select(pair => pair.Key).ToList();
			foreach (object badKey in nullRefs)
			{
				modifiers.Remove(badKey);
			}
		}

		/// <summary>
		/// Implicit float cast so that you don't have to call <see cref="GetValue"/> all the time.
		/// </summary>
		public static implicit operator float(CompositeFloatBase composite)
		{
			return composite.GetValue();
		}
	}
}
