using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Global service that broadcasts reported impacts.
	/// </summary>
	public class AwarenessService : IService
	{
		public event Action<ImpactData> ImpactEvent;

		private Dictionary<IEntity, Action<ImpactData>> listeners = new Dictionary<IEntity, Action<ImpactData>>();

		/// <summary>
		/// Adds a <see cref="IEntity"/> callback listener that gets invoked whenever the entity is victim of an impact.
		/// </summary>
		public void AddListener(IEntity entity, Action<ImpactData> callback)
		{
			if (!listeners.ContainsKey(entity))
			{
				listeners.Add(entity, callback);
			}
			else
			{
				SpaxDebug.Error("Could not add listener:", $"Entity [{entity.ID}] is already a listener.");
			}
		}

		public void RemoveListener(IEntity entity)
		{
			if (listeners.ContainsKey(entity))
			{
				listeners.Remove(entity);
			}
		}

		public void ReportImpact(ImpactData impact)
		{
			if (impact.Victim != null && listeners.ContainsKey(impact.Victim))
			{
				listeners[impact.Victim]?.Invoke(impact);
			}

			ImpactEvent?.Invoke(impact);
		}
	}
}
