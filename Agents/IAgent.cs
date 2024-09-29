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
		/// <summary>
		/// Invoked once the agent has died.
		/// </summary>
		event Action<IAgent> DiedEvent;

		/// <summary>
		/// Invoked once the agent has revived.
		/// </summary>
		event Action<IAgent> RevivedEvent;

		#region Properties

		/// <summary>
		/// The action performer of this agent.
		/// </summary>
		IActor Actor { get; }

		/// <summary>
		/// The state machine of this agent.
		/// </summary>
		IBrain Brain { get; }

		/// <summary>
		/// The desire manager of this agent, controls the <see cref="Brain"/>'s state when active.
		/// </summary>
		IMind Mind { get; }

		/// <summary>
		/// The relations manager of this agent, keeps track of relations for entity ID's and Labels.
		/// </summary>
		IRelations Relations { get; }

		/// <summary>
		/// The body of this agent, holds references to all physical data.
		/// </summary>
		IAgentBody Body { get; }

		/// <summary>
		/// The targetable component of this agent, contains all data on the physical dimensions of this entity.
		/// </summary>
		ITargetable Targetable { get; }

		/// <summary>
		/// The targeter of this agent, keeps track of the currently targeted entity.
		/// </summary>
		ITargeter Targeter { get; }

		#endregion

		/// <summary>
		/// Kills the agent, having it enter the "dead" state.
		/// </summary>
		void Die(ITransition transition = null);

		/// <summary>
		/// Revives the agent, having it exit the "dead" state.
		/// </summary>
		void Revive(ITransition transition = null);
	}
}
