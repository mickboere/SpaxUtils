using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="CompositeFloatBase"/> implementation that allows modifications to an entity's <see cref="RuntimeDataEntry"/>s.
	/// </summary>
	public class EntityStat : CompositeFloatBase
	{
		/// <summary>
		/// Links to the identifier of the attached <see cref="RuntimeDataEntry"/>.
		/// </summary>
		public string Identifier => data.ID;

		public override float BaseValue
		{
			get
			{
				return (float)data.Value;
			}
			set
			{
				if (!data.Value.Equals(value))
				{
					data.Value = value;
					// No need to call ValueChanged() here as that will happen automatically after the data's ValueChangedEvent.
				}
			}
		}

		private IEntity entity;
		private RuntimeDataEntry data;

		private float? minValue;
		private float? maxValue;
		private DecimalMethod decimals;

		private EntityStat damageMultiplier;

		public EntityStat(IEntity entity, RuntimeDataEntry data,
			Dictionary<object, IModifier<float>> modifiers = null,
			float? minValue = null, float? maxValue = null,
			DecimalMethod decimals = DecimalMethod.Decimal) : base(modifiers)
		{
			this.entity = entity;
			this.data = data;

			this.minValue = minValue;
			this.maxValue = maxValue;
			this.decimals = decimals;

			data.ValueChangedEvent += OnDataValueChanged;
		}

		public override float GetValue()
		{
			float value = base.GetValue();
			if (minValue.HasValue && value < minValue.Value)
			{
				value = minValue.Value;
			}
			if (maxValue.HasValue && value > maxValue.Value)
			{
				value = maxValue.Value;
			}

			switch (decimals)
			{
				case DecimalMethod.Floor:
					return Mathf.Floor(value);
				case DecimalMethod.Round:
					return Mathf.Round(value);
				case DecimalMethod.Ceil:
					return Mathf.Ceil(value);
				case DecimalMethod.Decimal:
				default:
					return value;
			}
		}

		public override void Dispose()
		{
			data.ValueChangedEvent -= OnDataValueChanged;
			base.Dispose();
		}

		public void Damage(float damage, bool clamp = true, bool applyMultiplier = true)
		{
			if (applyMultiplier)
			{
				damageMultiplier = damageMultiplier ?? entity?.GetStat(Identifier.SubStat(AgentStatIdentifiers.SUB_COST));
				damage *= damageMultiplier ?? 1f;
			}

			BaseValue = clamp ? Mathf.Max(0f, BaseValue - damage) : BaseValue - damage;
		}

		public void Damage(float damage, bool clamp, out bool drained, bool applyMultiplier = true)
		{
			if (applyMultiplier)
			{
				damageMultiplier = damageMultiplier ?? entity?.GetStat(Identifier.SubStat(AgentStatIdentifiers.SUB_COST));
				damage *= damageMultiplier ?? 1f;
			}

			drained = damage > BaseValue;
			Damage(damage, clamp, false);
		}

		public void Damage(float damage, bool clamp, out bool drained, out float excess, bool applyMultiplier = true)
		{
			if (applyMultiplier)
			{
				damageMultiplier = damageMultiplier ?? entity?.GetStat(Identifier.SubStat(AgentStatIdentifiers.SUB_COST));
				damage *= damageMultiplier ?? 1f;
			}

			excess = Mathf.Abs(Mathf.Min(0, BaseValue - damage));
			drained = excess > 0;
			Damage(damage, clamp, false);
		}

		private void OnDataValueChanged(object newValue)
		{
			ValueChanged();
		}
	}
}
