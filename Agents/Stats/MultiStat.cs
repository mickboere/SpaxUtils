using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class MultiStat
	{
		public string CurrentStat => currentStat;
		public string MaxStat => maxStat;
		public bool IsRecoverable => isRecoverable;
		public string RecoverableStat => recoverableStat;

		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string currentStat;
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string maxStat;
		[SerializeField, HideInInspector] private bool isRecoverable;
		[SerializeField, Conditional(nameof(isRecoverable), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string recoverableStat;
		[SerializeField, Conditional(nameof(isRecoverable), hide: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string frailtyStat;
		[SerializeField, HideInInspector] private bool hasRecovery;
		[SerializeField, Conditional(nameof(hasRecovery), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string recoveryStat;

		private EntityStat current;
		private EntityStat max;
		private EntityStat recoverable;
		private EntityStat frailty;
		private EntityStat recovery;

		private float lastCurrent;
		private float lastDamage;

		public void Initialize(IEntity entity)
		{
			current = entity.GetStat(currentStat, true);
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
			}
		}

		public void Update(float delta)
		{
			if (hasRecovery)
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
				// Return here as this change will reinvoke the callback.
				return;
			}

			if (current < lastCurrent)
			{
				// Damage has occured to the Current stat.
				lastDamage = lastCurrent - current;
				if (isRecoverable)
				{
					recoverable.BaseValue -= lastDamage * frailty;
				}
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
