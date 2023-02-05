using SpaxUtils.StateMachine;
using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Root <see cref="IEntity"/> implementation for free agents which can act, think, target and be targeted.
	/// Input has to be routed through the <see cref="Actor"/> as <see cref="IAct"/>s, which can be observed and reacted to.
	/// Other <see cref="IEntity"/>s can attach themselves to this entity to provide additional data and functionality.
	/// </summary>
	public interface IAgent : IEntity
	{
		#region Properties

		/// <summary>
		/// The <see cref="IActor"/> of this agent. Allows one to perform and observe <see cref="IAct"/>s.
		/// </summary>
		IActor Actor { get; }

		/// <summary>
		/// The <see cref="IBrain"/> of this agent.
		/// </summary>
		IBrain Brain { get; }

		/// <summary>
		/// The <see cref="IAgentBody"/> of this agent.
		/// </summary>
		IAgentBody Body { get; }

		/// <summary>
		/// The <see cref="ITargetable"/> to use when targeting this agent.
		/// </summary>
		ITargetable Targetable { get; }

		/// <summary>
		/// The <see cref="ITargeter"/> to use when requesting the objective of this agent.
		/// </summary>
		ITargeter Targeter { get; }

		#endregion
	}
}
