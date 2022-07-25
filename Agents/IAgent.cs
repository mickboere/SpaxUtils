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
		/// <summary>
		/// Invoked when a new entity is attached to this agent.
		/// </summary>
		event Action<IEntity> AttachedEntityEvent;

		/// <summary>
		/// Invoked when an entity is detached from this agent.
		/// </summary>
		event Action<IEntity> DetachedEntityEvent;

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
		/// All entities attached to this agent.
		/// </summary>
		List<IEntity> Attachments { get; }

		#endregion

		#region Methods

		/// <summary>
		/// (Re)Initialize this agent, giving it a new identification and restarting its brain.
		/// </summary>
		void Initialize(IIdentification identification, StateMachineGraph brainGraph = null);

		/// <summary>
		/// Attaches a new entity to this agent, allowing access to its data and components.
		/// </summary>
		void AttachEntity(IEntity entity);

		/// <summary>
		/// Detaches an entity from this agent, removing access to its data and components.
		/// </summary>
		void DetachEntity(IEntity entity);

		#endregion
	}
}
