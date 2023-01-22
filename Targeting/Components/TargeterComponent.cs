using SpaxUtils;
using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Implementation of <see cref="ITargeter"/>.
	/// Stores this entity's targets (as <see cref="ITargetable"/>) and its current main target.
	/// </summary>
	public class TargeterComponent : EntityComponentBase, ITargeter
	{
		/// <inheritdoc/>
		public event Action<ITargetable> TargetChangedEvent;

		#region Properties

		/// <inheritdoc/>
		public IList<ITargetable> Targets { get; private set; } = new List<ITargetable>();

		/// <inheritdoc/>
		public ITargetable Target { get; private set; }

		#endregion Properties

		#region Methods

		/// <inheritdoc/>
		public void AddTarget(ITargetable targetable)
		{
			if (targetable == null || Targets.Contains(targetable))
			{
				return;
			}

			Targets.Add(targetable);
		}

		/// <inheritdoc/>
		public void RemoveTarget(ITargetable targetable)
		{
			if (Target == targetable)
			{
				SetTarget(null);
			}

			if (Targets.Contains(targetable))
			{
				Targets.Remove(targetable);
			}
		}

		/// <inheritdoc/>
		public void SetTargets(IEnumerable<ITargetable> targetables)
		{
			Targets = new List<ITargetable>(targetables);

			if (Target != null && !Targets.Contains(Target))
			{
				Targets.Add(Target);
			}
		}

		/// <inheritdoc/>
		public void SetTarget(ITargetable targetable)
		{
			if (targetable != null && !targetable.IsTargetable)
			{
				SpaxDebug.Error("Can't set target", "Target isn't targetable.");
				targetable = null;
			}

			ITargetable previous = Target;
			AddTarget(targetable);
			Target = targetable;
			if (Target != previous)
			{
				TargetChangedEvent?.Invoke(Target);
			}
		}

		#endregion Methods
	}
}
