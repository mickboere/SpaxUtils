using System;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> interface for entities that can target a single <see cref="ITargetable"/>.
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
		/// The <see cref="ITargetable"/> currently being targetted.
		/// </summary>
		ITargetable Target { get; }

		/// <summary>
		/// Whether this targeter currently has a target.
		/// </summary>
		bool Targeting { get; }

		#endregion Properties

		#region Methods

		/// <summary>
		/// Sets the <see cref="Target"/>.
		/// </summary>
		void SetTarget(ITargetable targetable);

		#endregion Methods
	}
}
