using System;

namespace SpaxUtils
{
	public class StatSubscription : IDisposable
	{
		private EntityStat stat;
		private Action<EntityStat> callback;

		public StatSubscription(EntityStat stat, Action<EntityStat> callback)
		{
			this.stat = stat;
			this.callback = callback;

			stat.ValueChangedEvent += OnStatUpdate;
			callback(stat);
		}

		public void Dispose()
		{
			stat.ValueChangedEvent -= OnStatUpdate;
		}

		private void OnStatUpdate()
		{
			callback(stat);
		}
	}
}
