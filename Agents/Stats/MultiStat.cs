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
		[SerializeField, HideInInspector] private bool hasRecovery;
		[SerializeField, Conditional(nameof(hasRecovery), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string recoveryStat;

		private EntityStat current;
		private EntityStat max;
		private EntityStat recoverable;
		private EntityStat recovery;

		public void Initialize(IEntity entity)
		{
			current = entity.GetStat(currentStat, true);
			current.ValueChangedEvent += OnCurrentChangedEvent;

			max = entity.GetStat(maxStat, true);
			max.ValueChangedEvent += OnMaxChangedEvent;

			if (isRecoverable)
			{
				recoverable = entity.GetStat(recoverableStat, true);
				recoverable.ValueChangedEvent += OnRecoverableChangedEvent;
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
				current.BaseValue = Mathf.Min(recoverable, current.BaseValue + recovery * delta);
			}
		}

		public void Recover()
		{
			current.BaseValue = max;
		}

		private void OnCurrentChangedEvent()
		{
			current.BaseValue = Mathf.Min(current, max);

			if (isRecoverable)
			{
				recoverable.BaseValue = Mathf.Max(recoverable, current);
			}
		}

		private void OnMaxChangedEvent()
		{
			current.BaseValue = Mathf.Min(current, max);

			if (isRecoverable)
			{
				recoverable.BaseValue = Mathf.Min(recoverable, max);
			}
		}

		private void OnRecoverableChangedEvent()
		{
			recoverable.BaseValue = Mathf.Min(recoverable, max);
		}
	}
}
