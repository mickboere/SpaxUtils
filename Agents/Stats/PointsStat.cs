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
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string stat;
		[SerializeField] private bool isRecoverable;
		[SerializeField] private bool hasRecovery;

		private string maxStat;
		private string recoverableStat;
		private string frailtyStat;
		private string recoveryStat;
		private string recoveryDelayStat;

		private EntityStat current;
		private EntityStat max;
		private EntityStat recoverable;
		private EntityStat frailty;
		private EntityStat recovery;
		private EntityStat recoveryDelay;

		private float lastCurrent;
		private float lastDamage;

		private TimerStruct recoveryTimer;

		public void Initialize(IEntity entity)
		{
			maxStat = stat.SubStat(AgentStatIdentifiers.SUB_MAX);
			recoverableStat = stat.SubStat(AgentStatIdentifiers.SUB_RECOVERABLE);
			frailtyStat = stat.SubStat(AgentStatIdentifiers.SUB_FRAILTY);
			recoveryStat = stat.SubStat(AgentStatIdentifiers.SUB_RECOVERY);
			recoveryDelayStat = stat.SubStat(AgentStatIdentifiers.SUB_RECOVERY_DELAY);

			current = entity.GetStat(stat, true);
			current.ValueChangedEvent += OnCurrentChangedEvent;
			lastCurrent = current;

			max = entity.GetStat(maxStat, true);
			max.ValueChangedEvent += OnMaxChangedEvent;

			if (isRecoverable)
			{
				recoverable = entity.GetStat(recoverableStat, true);
				recoverable.ValueChangedEvent += OnRecoverableChangedEvent;
				frailty = entity.GetStat(frailtyStat);
			}
			if (hasRecovery)
			{
				recovery = entity.GetStat(recoveryStat, true);
				recoveryDelay = entity.GetStat(recoveryDelayStat, true);
			}
		}

		public void Update(float delta)
		{
			if (hasRecovery && !recoveryTimer)
			{
				if (isRecoverable)
				{
					// Recover Current towards Recoverable.
					current.BaseValue = Mathf.Min(recoverable, current.BaseValue + recovery * delta);
				}
				else
				{
					// Recover Current towards Max.
					current.BaseValue = Mathf.Min(max, current.BaseValue + recovery * delta);
				}
			}
		}

		public void Recover()
		{
			current.BaseValue = max;
		}

		private void OnCurrentChangedEvent()
		{
			if (current > max)
			{
				// Current cannot exceed Max.
				current.BaseValue = max;
				// Return here as this change will have reinvoked this callback.
				return;
			}

			if (current < lastCurrent)
			{
				// Damage has occured to the Current stat.
				lastDamage = lastCurrent - current;
				if (isRecoverable)
				{
					if(frailty != null)
					{
						// Subtract frailty damage from recoverable.
						recoverable.BaseValue -= lastDamage * frailty;
					}
					
					if (current < 0)
					{
						// Substract overdraw damage from recoverable.
						recoverable.BaseValue -= Mathf.Abs(current);
					}
				}

				// TODO: Make timer rely on entity timescale.
				recoveryTimer = new TimerStruct(recoveryDelay);
			}
			else if (isRecoverable)
			{
				// Current has healed, Recoverable cannot be smaller than Current.
				recoverable.BaseValue = Mathf.Max(recoverable, current);
			}

			lastCurrent = current;
		}

		private void OnMaxChangedEvent()
		{
			// Current cannot exceed Max.
			current.BaseValue = Mathf.Min(current, max);

			if (isRecoverable)
			{
				// Recoverable cannot exceed Max.
				recoverable.BaseValue = Mathf.Min(recoverable, max);
			}
		}

		private void OnRecoverableChangedEvent()
		{
			// Recoverable cannot exceed Max.
			recoverable.BaseValue = Mathf.Min(recoverable, max);
		}
	}
}
