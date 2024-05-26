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
		[SerializeField] private float overdraw;

		private string maxId;
		private string recoverableId;
		private string frailtyId;
		private string recoveryId;
		private string recoveryDelayId;

		private EntityStat timescale;
		private EntityStat current;
		private EntityStat max;
		private EntityStat recoverable;
		private EntityStat frailty;
		private EntityStat recovery;
		private EntityStat recoveryDelay;

		private float lastCurrent;
		private float lastDamage;

		private TimerClass recoveryTimer;

		public void Initialize(IEntity entity)
		{
			maxId = stat.SubStat(AgentStatIdentifiers.SUB_MAX);
			recoverableId = stat.SubStat(AgentStatIdentifiers.SUB_RECOVERABLE);
			frailtyId = stat.SubStat(AgentStatIdentifiers.SUB_FRAILTY);
			recoveryId = stat.SubStat(AgentStatIdentifiers.SUB_RECOVERY);
			recoveryDelayId = stat.SubStat(AgentStatIdentifiers.SUB_RECOVERY_DELAY);

			timescale = entity.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);

			current = entity.GetStat(stat, true);
			current.ValueChangedEvent += OnCurrentChangedEvent;
			lastCurrent = current;

			max = entity.GetStat(maxId, true);
			max.ValueChangedEvent += OnMaxChangedEvent;

			if (isRecoverable)
			{
				recoverable = entity.GetStat(recoverableId, true);
				recoverable.ValueChangedEvent += OnRecoverableChangedEvent;
				frailty = entity.GetStat(frailtyId);
			}
			if (hasRecovery)
			{
				recovery = entity.GetStat(recoveryId, true);
				recoveryDelay = entity.GetStat(recoveryDelayId, true);
			}
		}

		public void Update(float delta)
		{
			if (hasRecovery && (recoveryTimer == null || recoveryTimer.Expired))
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
			float current = this.current.BaseValue;

			if (current > max)
			{
				// Current cannot exceed Max.
				this.current.BaseValue = max;
				// Return here as this change will have reinvoked this callback.
				return;
			}

			if (current < lastCurrent)
			{
				// Damage has occured to the Current stat.
				lastDamage = lastCurrent - current;
				if (isRecoverable)
				{
					if (frailty != null)
					{
						// Subtract frailty damage from recoverable.
						recoverable.BaseValue -= lastDamage * frailty;
					}

					if (current < 0)
					{
						// Substract overdraw damage from recoverable.
						if (lastCurrent < 0)
						{
							recoverable.BaseValue -= lastDamage * overdraw;
						}
						else
						{
							recoverable.BaseValue -= Mathf.Abs(current) * overdraw;
						}
					}
				}

				// TODO: Make timer rely on entity timescale.
				float duration = current < Mathf.Epsilon ? recoveryDelay * 2f : recoveryDelay;
				recoveryTimer = recoveryTimer?.Reset(duration) ?? new TimerClass(duration, () => timescale, true);
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
