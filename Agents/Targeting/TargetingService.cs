using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Global service that tracks which <see cref="ITargeter"/>s are currently pointing at which <see cref="ITargetable"/>s.
	/// Call <see cref="Register"/> from <see cref="TargeterComponent.OnEnable"/> and <see cref="Unregister"/> from OnDisable.
	/// </summary>
	public class TargetingService : IService
	{
		// Reverse lookup: targetable → set of targeters pointing at it.
		private readonly Dictionary<ITargetable, HashSet<ITargeter>> byTarget = new Dictionary<ITargetable, HashSet<ITargeter>>();

		// Per-targeter unsubscribe lambdas (one per registered targeter).
		private readonly Dictionary<ITargeter, Action<ITargetable>> handlers = new Dictionary<ITargeter, Action<ITargetable>>();

		// Per-targeter current target (needed to remove from old bucket on change).
		private readonly Dictionary<ITargeter, ITargetable> current = new Dictionary<ITargeter, ITargetable>();

		public void Register(ITargeter targeter)
		{
			if (handlers.ContainsKey(targeter))
				return;

			Action<ITargetable> handler = (newTarget) => OnTargetChanged(targeter, newTarget);
			handlers[targeter] = handler;
			targeter.TargetChangedEvent += handler;

			ITargetable initialTarget = targeter.Target;
			current[targeter] = initialTarget;
			if (initialTarget != null)
				AddToSet(initialTarget, targeter);
		}

		public void Unregister(ITargeter targeter)
		{
			if (!handlers.TryGetValue(targeter, out Action<ITargetable> handler))
				return;

			targeter.TargetChangedEvent -= handler;
			handlers.Remove(targeter);

			if (current.TryGetValue(targeter, out ITargetable prev) && prev != null)
				RemoveFromSet(prev, targeter);

			current.Remove(targeter);
		}

		public IEnumerable<ITargeter> GetTargeters(ITargetable targetable)
		{
			if (targetable != null && byTarget.TryGetValue(targetable, out HashSet<ITargeter> set))
				return set;
			return System.Linq.Enumerable.Empty<ITargeter>();
		}

		public int TargeterCount(ITargetable targetable)
		{
			if (targetable != null && byTarget.TryGetValue(targetable, out HashSet<ITargeter> set))
				return set.Count;
			return 0;
		}

		public bool IsBeingTargeted(ITargetable targetable)
		{
			return targetable != null && byTarget.TryGetValue(targetable, out HashSet<ITargeter> set) && set.Count > 0;
		}

		private void OnTargetChanged(ITargeter targeter, ITargetable newTarget)
		{
			if (current.TryGetValue(targeter, out ITargetable prev) && prev != null)
				RemoveFromSet(prev, targeter);

			current[targeter] = newTarget;
			if (newTarget != null)
				AddToSet(newTarget, targeter);
		}

		private void AddToSet(ITargetable targetable, ITargeter targeter)
		{
			if (!byTarget.TryGetValue(targetable, out HashSet<ITargeter> set))
			{
				set = new HashSet<ITargeter>();
				byTarget[targetable] = set;
			}
			set.Add(targeter);
		}

		private void RemoveFromSet(ITargetable targetable, ITargeter targeter)
		{
			if (!byTarget.TryGetValue(targetable, out HashSet<ITargeter> set))
				return;
			set.Remove(targeter);
			if (set.Count == 0)
				byTarget.Remove(targetable);
		}
	}
}
