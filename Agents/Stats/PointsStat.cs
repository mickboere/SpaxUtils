using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SpaxUtils
{
	/// <summary>
	/// Class that manages a stat which defines points. This class will handle capping, damage recovery and all other sub-stats related to points.
	/// </summary>
	[Serializable]
	public class PointsStat
	{
		#region Tooltips
		private const string TT_defaultIsFull = "When true, a full recovery of the agent will ensure the stat is also fully recovered.";
		private const string TT_hasRecovery = "Whether this stat automatically recovers points over time.";
		private const string TT_isRecoverable = "Whether this stat has a diminishable recoverable amount separate from its max amount.";
		private const string TT_overdraw = "The amount taxed from the recoverable amount when the stat is overdrawn (below 0). Default: 1.";
		#endregion

		public string Identifier => stat;
		public EntityStat Current { get; private set; }
		public EntityStat Max { get; private set; }
		public EntityStat GainMult { get; private set; }
		public EntityStat DrainMult { get; private set; }
		public EntityStat Reserve { get; private set; }
		public EntityStat Recovery { get; private set; }
		public EntityStat RecoveryDelay { get; private set; }
		public EntityStat Frailty { get; private set; }
		public EntityStat Exp { get; private set; }
		public EntityStat ExpGainMult { get; private set; }

		public bool DefaultIsFull => defaultIsFull;
		public bool HasRecovery => hasRecovery;
		public bool HasReserve => hasReserve;

		/// <summary>
		/// The current value of the Current stat's base value.
		/// </summary>
		public float Value { get { return Current.BaseValue; } protected set { Current.BaseValue = value; } }

		/// <summary>
		/// How much the curent points make up of the max points.
		/// </summary>
		public float PercentageMax => Current / Max;

		/// <summary>
		/// How much the current points make up of the recoverable points (if no reserve, max).
		/// </summary>
		public float PercentageRecoverable => HasReserve ? Current / Reserve : PercentageMax;

		/// <summary>
		/// How much the recoverable points make up of the max points.
		/// </summary>
		public float ReservePercentage => HasReserve ? Reserve / Max : 1f;

		/// <summary>
		/// Whether the stat points are currently recovering.
		/// </summary>
		public bool IsRecovering => HasRecovery && (recoveryTimer == null || recoveryTimer.Expired) && !PercentageRecoverable.Approx(1f);

		/// <summary>
		/// Whether the stat points are currently recovering after having been drained completely.
		/// </summary>
		public bool IsRecoveringFromZero => wasDrained && (Current.BaseValue.Approx(0f) || IsRecovering);

		[SerializeField, ConstDropdown(typeof(IStatIdentifiers), includeEmpty: true)] private string stat;
		[SerializeField, Tooltip(TT_defaultIsFull)] private bool defaultIsFull = true;
		[SerializeField, Tooltip(TT_hasRecovery)] private bool hasRecovery;
		[SerializeField] private float drainedRecoveryDelayPenalty = 1.5f;
		[SerializeField, Tooltip(TT_isRecoverable)] private bool hasReserve;
		[SerializeField, Conditional(nameof(hasReserve), hide: true), Tooltip(TT_overdraw)] private float overdraw = 0f;

		[SerializeField, ConstDropdown(typeof(IStatIdentifiers), includeEmpty: true)] private string expStat;
		[SerializeField] private float expGainMultiplier = 1f;
		[SerializeField] private float expDrainMultiplier = 1f;

		private bool initialized = false;
		private EntityStat timescale;
		private float lastCurrent;
		private float lastOverdraw;
		private bool wasDrained;

		private TimerClass recoveryTimer;

		// When true, the next Current.ValueChanged callback is considered an internal clamp write and should not grant EXP or run damage/heal logic.
		private bool suppressNextReward;

		public void Initialize(IEntity entity)
		{
			if (string.IsNullOrEmpty(stat))
			{
				SpaxDebug.Warning($"{entity.Identification.ID}: Could not initialize PointStat", $"Stat identifier is NULL.");
				return;
			}

			timescale = entity.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);

			Current = entity.Stats.GetStat(stat, true);

			lastCurrent = Current;
			Current.ValueChangedEvent += OnCurrentChangedEvent;

			Max = entity.Stats.GetStat(stat.SubStat(AgentStatIdentifiers.SUB_MAX), true);
			Max.ValueChangedEvent += OnMaxChangedEvent;

			GainMult = entity.Stats.GetStat(stat.SubStat(AgentStatIdentifiers.SUB_GAIN), true, 1f);
			DrainMult = entity.Stats.GetStat(stat.SubStat(AgentStatIdentifiers.SUB_DRAIN), true, 1f);

			if (HasReserve)
			{
				Reserve = entity.Stats.GetStat(stat.SubStat(AgentStatIdentifiers.SUB_RESERVE), true);
				Reserve.ValueChangedEvent += OnRecoverableChangedEvent;
				Frailty = entity.Stats.GetStat(stat.SubStat(AgentStatIdentifiers.SUB_FRAILTY));
			}
			if (HasRecovery)
			{
				Recovery = entity.Stats.GetStat(stat.SubStat(AgentStatIdentifiers.SUB_RECOVERY), true);
				RecoveryDelay = entity.Stats.GetStat(stat.SubStat(AgentStatIdentifiers.SUB_RECOVERY_DELAY), true);
			}

			if (Value > Max)
			{
				// Points have been manually supplied through data, accomodate for it.
				Max.BaseValue = Value - Max; // Account for Max modifiers.
				if (hasReserve)
				{
					Reserve.BaseValue = Max;
				}
			}

			if (!string.IsNullOrEmpty(expStat))
			{
				Exp = entity.Stats.GetStat(expStat, true);
				ExpGainMult = entity.Stats.GetStat(expStat.SubStat(AgentStatIdentifiers.SUB_GAIN), true, 1f);
			}

			initialized = true;
		}

		public void Update(float delta)
		{
			if (!initialized)
			{
				return;
			}

			if (HasRecovery && (recoveryTimer == null || recoveryTimer.Expired))
			{
				float cap = HasReserve ? (float)Reserve : (float)Max;
				float target = Mathf.Min(cap, Current.BaseValue + Recovery * delta * timescale);
				if (target > Current.BaseValue)
				{
					Set(target, true);
				}
			}
		}

		/// <summary>
		/// Sets the Current's BaseValue to the target value.
		/// If rewardsEXP is true, the delta between previous and new value will be rewarded as EXP.
		/// </summary>
		/// <returns>The delta between the previous and the current value.</returns>
		public float Set(float target, bool rewardsEXP)
		{
			suppressNextReward = !rewardsEXP;
			float previous = Value;
			Value = target;
			// <OnCurrentChangedEvent has now been called, clamping the Value>
			return Value - previous;
		}

		/// <summary>
		/// Adds points to the Current stat, optionally applying the implicit Gain multiplier.
		/// </summary>
		/// <returns>Returns the amount that was actually gained.</returns>
		public float Gain(float amount, bool applyMultiplier = true)
		{
			amount *= applyMultiplier ? GainMult : 1f;
			return Set(Current.BaseValue + amount, true);
		}

		/// <summary>
		/// Subtracts points from the Current stat, optionally applying the implicit Drain multiplier.
		/// </summary>
		/// <returns>Returns the amount that was actually drained.</returns>
		public float Drain(float amount, bool applyMultiplier = true)
		{
			amount *= applyMultiplier ? DrainMult : 1f;
			return -Set(Current.BaseValue - amount, true);
		}

		/// <summary>
		/// Subtracts points from the Current stat, optionally applying the implicit Drain multiplier.
		/// Has out parameters to indicate if fully drained and any overdraw amount.
		/// </summary>
		/// <returns>Returns the amount that was actually drained (amount-overdraw).</returns>
		public float Drain(float amount, out bool drained, bool applyMultiplier = true)
		{
			float damage = Drain(amount, applyMultiplier);
			drained = Value.Approx(0f);
			return damage;
		}

		/// <summary>
		/// Subtracts points from the Current stat, optionally applying the implicit Drain multiplier.
		/// Has out parameters to indicate if fully drained and any overdraw amount.
		/// </summary>
		/// <returns>Returns the amount that was actually drained (amount-overdraw).</returns>
		public float Drain(float amount, out bool drained, out float overdraw, bool applyMultiplier = true)
		{
			lastOverdraw = 0f;
			float damage = Drain(amount, applyMultiplier);
			drained = Value.Approx(0f);
			overdraw = lastOverdraw;
			return damage;
		}

		/// <summary>
		/// Sets the current value to the max value.
		/// </summary>
		public float Recover()
		{
			return Set(Max, false);
		}

		private void OnCurrentChangedEvent()
		{
			float current = Value;
			float damage = lastCurrent - current;

			// Calculate overdraw before clamping.
			if (current < 0)
			{
				lastOverdraw = Mathf.Abs(current);
				// Apply overdraw damage to Reserve.
				if (HasReserve && overdraw > 0f)
				{
					Reserve.BaseValue -= lastOverdraw * overdraw;
				}
			}

			// Clamp Current between 0 and Max.
			if (current < 0f || current > Max)
			{
				Value = Mathf.Clamp(Value, 0f, Max);
				// OnChanged is reinvoked by the above line, exit this call.
				return;
			}

			// Reward EXP for the change in Current, unless suppressed.
			if (suppressNextReward) suppressNextReward = false;
			else RewardExp(damage);

			// Check if no longer drained.
			if (PercentageRecoverable.Approx(1f))
			{
				wasDrained = false;
			}

			// Handle damage.
			if (damage > 0f)
			{
				if (current.Approx(0f))
				{
					wasDrained = true;
				}

				if (HasReserve)
				{
					if (Frailty != null)
					{
						// Subtract frailty damage from reserve.
						Reserve.BaseValue -= damage * Frailty;
					}
				}

				if (HasRecovery)
				{
					float duration = wasDrained ? RecoveryDelay * drainedRecoveryDelayPenalty : RecoveryDelay;
					recoveryTimer = recoveryTimer?.Reset(duration) ?? new TimerClass(duration, () => timescale, true);
				}
			}
			else if (HasReserve && current > Reserve)
			{
				// Current has healed, Recoverable cannot be smaller than Current.
				Reserve.BaseValue = current;
			}

			lastCurrent = current;
		}

		private void RewardExp(float damage)
		{
			if (Exp == null)
			{
				return;
			}

			float mult = damage >= 0f ? expDrainMultiplier : expGainMultiplier;
			Exp.BaseValue += damage.Abs() * mult * ExpGainMult;
		}

		private void OnMaxChangedEvent()
		{
			// Current cannot exceed Max. This clamp should not grant EXP or count as an action.
			float clamped = Mathf.Min(Current, Max);
			if (!Current.BaseValue.Approx(clamped))
			{
				suppressNextReward = true;
				Current.BaseValue = clamped;
				lastCurrent = clamped;
			}

			if (HasReserve)
			{
				// Recoverable cannot exceed Max.
				Reserve.BaseValue = Mathf.Min(Reserve, Max);
			}
		}

		private void OnRecoverableChangedEvent()
		{
			// Recoverable cannot exceed Max.
			Reserve.BaseValue = Mathf.Min(Reserve, Max);
		}

		public static implicit operator float(PointsStat pointsStat)
		{
			return pointsStat.Current ?? 0f;
		}
	}
}
