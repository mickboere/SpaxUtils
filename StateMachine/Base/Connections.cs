using System;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Class containing the different connection types of state machine nodes.
	/// </summary>
	public static class Connections
	{
		/// <summary>
		/// Connects to a State.
		/// </summary>
		[Serializable]
		public class State { }

		/// <summary>
		/// Connects to a Component.
		/// </summary>
		[Serializable]
		public class StateComponent { }

		/// <summary>
		/// Connects to a Rule.
		/// </summary>
		[Serializable]
		public class Rule { }
	}
}