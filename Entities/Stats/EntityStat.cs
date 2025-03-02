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
		public string Identifier => Data.ID;

		public RuntimeDataEntry Data { get; private set; }

		public override float BaseValue
		{
			get
			{
				return (float)Data.Value;
			}
			set
			{
				float clamp = ClampValue(value);
				if (!Data.Value.Equals(clamp))
				{
					Data.Value = clamp;
					// No need to call ValueChanged() here as that will happen automatically after the data's ValueChangedEvent.
				}
			}
		}

		private IEntity entity;
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
			this.Data = data;

			this.minValue = minValue;
			this.maxValue = maxValue;
			this.decimals = decimals;

			data.ValueChangedEvent += OnDataValueChanged;
		}

		public override float GetValue()
		{
			return ClampValue(base.GetValue()).Decimal(decimals);
		}

		public override void Dispose()
		{
			Data.ValueChangedEvent -= OnDataValueChanged;
			base.Dispose();
		}

		#region Damage

		public float Damage(float damage, bool clamp = true, bool applyMultiplier = true)
		{
			CalculateDamage(ref damage, applyMultiplier);
			BaseValue = clamp ? Mathf.Max(0f, BaseValue - damage) : BaseValue - damage;
			return damage;
		}

		public float Damage(float damage, bool clamp, out bool drained, bool applyMultiplier = true)
		{
			CalculateDamage(ref damage, applyMultiplier);
			drained = damage > BaseValue;
			return Damage(damage, clamp, false);
		}

		public float Damage(float damage, bool clamp, out bool drained, out float overdraw, bool applyMultiplier = true)
		{
			CalculateDamage(ref damage, applyMultiplier);
			overdraw = Mathf.Abs(Mathf.Min(0, BaseValue - damage));
			drained = overdraw > 0;
			return Damage(damage, clamp, false);
		}

		private void CalculateDamage(ref float damage, bool applyMultiplier)
		{
			if (applyMultiplier)
			{
				damageMultiplier = damageMultiplier ?? entity?.Stats.GetStat(Identifier.SubStat(AgentStatIdentifiers.SUB_COST));
				damage *= damageMultiplier ?? 1f;
			}
		}

		#endregion Damage

		private float ClampValue(float value)
		{
			if (minValue.HasValue && value < minValue.Value)
			{
				value = minValue.Value;
			}
			else if (maxValue.HasValue && value > maxValue.Value)
			{
				value = maxValue.Value;
			}
			return value;
		}

		private void OnDataValueChanged(object newValue)
		{
			ValueChanged();
		}
	}
}
