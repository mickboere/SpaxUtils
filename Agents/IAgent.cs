using SpaxUtils.StateMachines;
using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Advanced <see cref="IEntity"/> implementation for free agents which have a body that can act, think, die, target and be targeted.
	/// Input has to be routed through the <see cref="Actor"/> as <see cref="IAct"/>s, which can be observed and reacted to.
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

		/// <summary>
		/// Whether this agent is currently dead.
		/// </summary>
		bool Dead { get; }

		#endregion

		/// <summary>
		/// Kills the agent, having it enter the "dead" state.
		/// </summary>
		void Die(ITransition transition = null);
	}
}
