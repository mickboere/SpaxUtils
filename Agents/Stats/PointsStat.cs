using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that manages a stat which defines points. This class will handle capping, damage recovery and all other sub-stats related to points.
	/// </summary>
	[Serializable]
	public class PointsStat
	{
		#region Tooltips
		private const string TT_hasRecovery = "Whether this stat automatically recovers points over time.";
		private const string TT_isRecoverable = "Whether this stat has a diminishable recoverable amount separate from its max amount.";
		private const string TT_overdraw = "The amount taxed from the recoverable amount when the stat is overdrawn (below 0). Default: 1.";
		#endregion

		public string Identifier => stat;
		public EntityStat Current { get; private set; }
		public EntityStat Max { get; private set; }
		public EntityStat Cost { get; private set; }
		public EntityStat Recoverable { get; private set; }
		public EntityStat Recovery { get; private set; }
		public EntityStat RecoveryDelay { get; private set; }
		public EntityStat Frailty { get; private set; }

		public float PercentileMax => Current / Max;
		public float PercentileRecoverable => isRecoverable ? Current / Recoverable : PercentileMax;
		public float RecoverablePercentile => isRecoverable ? Recoverable / Max : 1f;
		public bool IsRecovering => hasRecovery && (recoveryTimer == null || recoveryTimer.Expired) && !PercentileRecoverable.Approx(1f);

		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants), includeEmpty: true)] private string stat;
		[SerializeField, Tooltip(TT_hasRecovery)] private bool hasRecovery;
		[SerializeField, Tooltip(TT_isRecoverable)] private bool isRecoverable;
		[SerializeField, Conditional(nameof(isRecoverable), hide: true), Tooltip(TT_overdraw)] private float overdraw = 1f;

		private bool initialized = false;
		private EntityStat timescale;
		private float lastCurrent;
		private float lastDamage;

		private TimerClass recoveryTimer;

		public void Initialize(IEntity entity)
		{
			if (string.IsNullOrEmpty(stat))
			{
				SpaxDebug.Warning($"{entity.Identification.ID}: Could not initialize PointStat", $"Stat identifier is NULL.");
				return;
			}

			string maxId = stat.SubStat(AgentStatIdentifiers.SUB_MAX);
			string costId = stat.SubStat(AgentStatIdentifiers.SUB_COST);
			string recoverableId = stat.SubStat(AgentStatIdentifiers.SUB_RECOVERABLE);
			string frailtyId = stat.SubStat(AgentStatIdentifiers.SUB_FRAILTY);
			string recoveryId = stat.SubStat(AgentStatIdentifiers.SUB_RECOVERY);
			string recoveryDelayId = stat.SubStat(AgentStatIdentifiers.SUB_RECOVERY_DELAY);

			timescale = entity.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);

			Current = entity.GetStat(stat, true);
			Current.ValueChangedEvent += OnCurrentChangedEvent;
			lastCurrent = Current;

			Max = entity.GetStat(maxId, true);
			Max.ValueChangedEvent += OnMaxChangedEvent;

			Cost = entity.GetStat(costId, true, 1f);

			if (isRecoverable)
			{
				Recoverable = entity.GetStat(recoverableId, true);
				Recoverable.ValueChangedEvent += OnRecoverableChangedEvent;
				Frailty = entity.GetStat(frailtyId);
			}
			if (hasRecovery)
			{
				Recovery = entity.GetStat(recoveryId, true);
				RecoveryDelay = entity.GetStat(recoveryDelayId, true);
			}

			initialized = true;
		}

		public void Update(float delta)
		{
			if (!initialized)
			{
				return;
			}

			if (hasRecovery && (recoveryTimer == null || recoveryTimer.Expired))
			{
				if (isRecoverable)
				{
					// Recover Current towards Recoverable.
					Current.BaseValue = Mathf.Min(Recoverable, Current.BaseValue + Recovery * delta * timescale);
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
			float current = this.Current.BaseValue;

			if (current > Max)
			{
				// Current cannot exceed Max.
				this.Current.BaseValue = Max;
				// Return here as this change will have reinvoked this callback.
				return;
			}

			if (current < lastCurrent)
			{
				// Damage has occured to the Current stat.
				lastDamage = lastCurrent - current;
				if (isRecoverable)
				{
					if (Frailty != null)
					{
						// Subtract frailty damage from recoverable.
						Recoverable.BaseValue -= lastDamage * Frailty;
					}

					if (current < 0)
					{
						// Substract overdraw damage from recoverable.
						if (lastCurrent < 0)
						{
							Recoverable.BaseValue -= lastDamage * overdraw;
						}
						else
						{
							Recoverable.BaseValue -= Mathf.Abs(current) * overdraw;
						}
					}
				}

				float duration = current < Mathf.Epsilon ? RecoveryDelay * 1.5f : RecoveryDelay;
				recoveryTimer = recoveryTimer?.Reset(duration) ?? new TimerClass(duration, () => timescale, true);
			}
			else if (isRecoverable)
			{
				// Current has healed, Recoverable cannot be smaller than Current.
				Recoverable.BaseValue = Mathf.Max(Recoverable, current);
			}

			lastCurrent = current;
		}

		private void OnMaxChangedEvent()
		{
			// Current cannot exceed Max.
			Current.BaseValue = Mathf.Min(Current, Max);

			if (isRecoverable)
			{
				// Recoverable cannot exceed Max.
				Recoverable.BaseValue = Mathf.Min(Recoverable, Max);
			}
		}

		private void OnRecoverableChangedEvent()
		{
			// Recoverable cannot exceed Max.
			Recoverable.BaseValue = Mathf.Min(Recoverable, Max);
		}

		public static implicit operator float(PointsStat pointsStat)
		{
			return pointsStat.Current;
		}
	}
}
