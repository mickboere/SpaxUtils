using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for an object that is able to intercept interactions of specific types.
	/// </summary>
	public interface IInteractionBlocker
	{
		/// <summary>
		/// The types of interactions this blocks.
		/// Leave null or empty to block all interactions.
		/// </summary>
		IList<string> BlocksTypes { get; }

		/// <summary>
		/// Is this blocker currently blocking.
		/// </summary>
		bool Blocking { get; }

		/// <summary>
		/// The reason for blocking these interactions.
		/// </summary>
		string BlockReason { get; }
	}
}
