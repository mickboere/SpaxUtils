using SpaxUtils;
using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Implementation of <see cref="ITargeter"/>.
	/// Stores an entity's current target as <see cref="ITargetable"/>.
	/// </summary>
	public class TargeterComponent : EntityComponentBase, ITargeter
	{
		/// <inheritdoc/>
		public event Action<ITargetable> TargetChangedEvent;

		#region Properties

		/// <inheritdoc/>
		public ITargetable Target { get; private set; }

		/// <inheritdoc/>
		public bool Targeting => Target != null;

		#endregion Properties

		/// <inheritdoc/>
		public void SetTarget(ITargetable targetable)
		{
			if (targetable != null && !targetable.IsTargetable)
			{
				SpaxDebug.Error("Can't set target", "Target isn't targetable.");
				targetable = null;
			}

			if (Target == targetable)
			{
				return;
			}

			Target = targetable;
			TargetChangedEvent?.Invoke(Target);
		}
	}
}
