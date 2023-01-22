using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> for entities that can be target one or multiple <see cref="ITargetable"/>.
	/// </summary>
	public interface ITargeter : IEntityComponent
	{
		#region Events

		/// <summary>
		/// Invoked whenever the <see cref="Target"/> has changed.
		/// </summary>
		event Action<ITargetable> TargetChangedEvent;

		#endregion Events

		#region Properties

		/// <summary>
		/// All of the <see cref="ITargetable"/>s this <see cref="ITargeter"/> is currently aware of.
		/// </summary>
		//IList<ITargetable> Targets { get; }

		/// <summary>
		/// The <see cref="ITargetable"/> currently being targetted.
		/// </summary>
		ITargetable Target { get; }

		#endregion Properties

		#region Methods

		/// <summary>
		/// Adds the <paramref name="targetable"/> to the <see cref="Targets"/> list.
		/// </summary>
		//void AddTarget(ITargetable targetable);

		/// <summary>
		/// Removes the <paramref name="targetable"/> from the <see cref="Targets"/> list.
		/// </summary>
		//void RemoveTarget(ITargetable targetable);

		/// <summary>
		/// Replace the list of targets with <paramref name="targetables"/>.
		/// </summary>
		//void SetTargets(IEnumerable<ITargetable> targetables);

		/// <summary>
		/// Sets the <see cref="Target"/>.
		/// </summary>
		void SetTarget(ITargetable targetable);

		#endregion Methods
	}
}
