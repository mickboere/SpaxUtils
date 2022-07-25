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
		public event Action<ITargetable> CurrentTargetChangedEvent;

		#region Properties

		/// <inheritdoc/>
		public IList<ITargetable> Targets { get; private set; } = new List<ITargetable>();

		/// <inheritdoc/>
		public ITargetable CurrentTarget { get; private set; }

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
			if (CurrentTarget == targetable)
			{
				StopTargetting();
			}

			if (Targets.Contains(targetable))
			{
				Targets.Remove(targetable);
			}
		}

		/// <inheritdoc/>
		public void SetCurrentTarget(ITargetable targetable)
		{
			if (!targetable.IsTargetable)
			{
				SpaxDebug.Error("Can't set target", "Target isn't targetable.");
				targetable = null;
			}

			ITargetable previous = CurrentTarget;
			AddTarget(targetable);
			CurrentTarget = targetable;
			if (CurrentTarget != previous)
			{
				CurrentTargetChangedEvent?.Invoke(CurrentTarget);
			}
		}

		/// <inheritdoc/>
		public void StopTargetting()
		{
			CurrentTarget = null;
		}

		#endregion Methods
	}
}
