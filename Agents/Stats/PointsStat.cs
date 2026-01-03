using System;
using UnityEngine;
using UnityEngine.Serialization;

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
		public EntityStat Cost { get; private set; }
		public EntityStat Reserve { get; private set; }
		public EntityStat Recovery { get; private set; }
		public EntityStat RecoveryDelay { get; private set; }
		public EntityStat Frailty { get; private set; }

		public bool DefaultIsFull => defaultIsFull;
		public bool HasRecovery => hasRecovery;
		public bool HasReserve => hasReserve;

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
		[SerializeField, Tooltip(TT_isRecoverable), FormerlySerializedAs("isRecoverable")] private bool hasReserve;
		[SerializeField, Conditional(nameof(hasReserve), hide: true), Tooltip(TT_overdraw)] private float overdraw = 1f;

		private bool initialized = false;
		private EntityStat timescale;
		private float lastCurrent;
		private float lastDamage;
		private bool wasDrained;

		private TimerClass recoveryTimer;

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

			Cost = entity.Stats.GetStat(stat.SubStat(AgentStatIdentifiers.SUB_COST), true, 1f);

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

			if (Current.BaseValue > Max)
			{
				// Health points have been manually supplied through data, accomodate for it.
				Max.BaseValue = Current.BaseValue - Max; // Account for Max modifiers.
				if (hasReserve)
				{
					Reserve.BaseValue = Max;
				}
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
				if (HasReserve)
				{
					// Recover Current towards Recoverable.
					Current.BaseValue = Mathf.Min(Reserve, Current.BaseValue + Recovery * delta * timescale);
				}
				else
				{
					// Recover Current towards Max.
					Current.BaseValue = Mathf.Min(Max, Current.BaseValue + Recovery * delta * timescale);
				}
			}
		}

		/// <summary>
		/// Set the stat's current value to its max value.
		/// </summary>
		public void Recover()
		{
			if (!initialized)
			{
				return;
			}

			Current.BaseValue = Max;
		}

		private void OnCurrentChangedEvent()
		{
			float current = Current.BaseValue;
			if (current > Max)
			{
				// Current cannot exceed Max.
				this.Current.BaseValue = Max;
				// Return here as this change will have reinvoked this callback.
				return;
			}

			if (PercentageRecoverable.Approx(1f))
			{
				wasDrained = false;
			}

			if (current < lastCurrent)
			{
				// Damage has occured to the Current stat.
				lastDamage = lastCurrent - current;

				if (current < 0f || current.Approx(0f))
				{
					wasDrained = true;
				}

				if (HasReserve)
				{
					if (Frailty != null)
					{
						// Subtract frailty damage from recoverable.
						Reserve.BaseValue -= lastDamage * Frailty;
					}

					if (current <= 0)
					{
						// Substract overdraw damage from recoverable.
						if (lastCurrent < 0)
						{
							Reserve.BaseValue -= lastDamage * overdraw;
						}
						else
						{
							Reserve.BaseValue -= Mathf.Abs(current) * overdraw;
						}
					}
				}

				if (HasRecovery)
				{
					float duration = current < Mathf.Epsilon ? RecoveryDelay * 1.5f : RecoveryDelay;
					recoveryTimer = recoveryTimer?.Reset(duration) ?? new TimerClass(duration, () => timescale, true);
				}
			}
			else if (HasReserve)
			{
				// Current has healed, Recoverable cannot be smaller than Current.
				Reserve.BaseValue = Mathf.Max(Reserve, current);
			}

			lastCurrent = current;
		}

		private void OnMaxChangedEvent()
		{
			// Current cannot exceed Max.
			Current.BaseValue = Mathf.Min(Current, Max);

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
