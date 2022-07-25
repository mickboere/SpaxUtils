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
		/// Invoked whenever the <see cref="CurrentTarget"/> has changed.
		/// </summary>
		event Action<ITargetable> CurrentTargetChangedEvent;

		#endregion Events

		#region Properties

		/// <summary>
		/// All of the <see cref="ITargetable"/>s this <see cref="ITargeter"/> is currently aware of.
		/// </summary>
		IList<ITargetable> Targets { get; }

		/// <summary>
		/// The <see cref="ITargetable"/> currently being targetted.
		/// </summary>
		ITargetable CurrentTarget { get; }

		/// <summary>
		/// The Interest of this Targeter towards its <see cref="CurrentTarget"/>.
		/// Negative interest could be considered Aggro.
		/// </summary>
		//float Interest { get; } <- Should be Labeled Data in the entity instead.

		#endregion Properties

		#region Methods

		/// <summary>
		/// Adds the <paramref name="targetable"/> to the <see cref="Targets"/> list.
		/// </summary>
		void AddTarget(ITargetable targetable);

		/// <summary>
		/// Removes the <paramref name="targetable"/> from the <see cref="Targets"/> list.
		/// </summary>
		void RemoveTarget(ITargetable targetable);

		/// <summary>
		/// Sets the <see cref="CurrentTarget"/>.
		/// </summary>
		void SetCurrentTarget(ITargetable targetable);

		/// <summary>
		/// Sets the <see cref="CurrentTarget"/> to null.
		/// </summary>
		void StopTargetting();

		#endregion Methods
	}
}
