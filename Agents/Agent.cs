using SpaxUtils.StateMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Base <see cref="IAgent"/> implementation.
	/// </summary>
	public class Agent : Entity, IAgent, IDependencyProvider
	{
		/// <inheritdoc/>
		public IActor Actor { get; } = new Actor();

		/// <inheritdoc/>
		public IBrain Brain { get; private set; }

		/// <inheritdoc/>
		public IAgentBody Body { get; private set; }

		/// <inheritdoc/>
		public ITargetable Targetable { get; private set; }

		/// <inheritdoc/>
		public ITargeter Targeter { get; private set; }

		protected override string GameObjectNamePrefix => "[Agent]";

		[SerializeField, ConstDropdown(typeof(IStateIdentifierConstants))] private string state;
		[SerializeField] private StateMachineGraph[] brainGraphs;

		/// <inheritdoc/>
		public Dictionary<object, object> RetrieveDependencies()
		{
			var dependencies = new Dictionary<object, object>();
			dependencies.Add(typeof(Actor), Actor);
			return dependencies;
		}

		public void InjectDependencies(IAgentBody body, ITargetable targetableComponent, ITargeter targeterComponent,
			IPerformer[] performers, CallbackService callbackService)
		{
			Body = body;
			Targetable = targetableComponent;
			Targeter = targeterComponent;

			foreach (IPerformer performer in performers)
			{
				if (performer != Actor)
				{
					Actor.AddPerformer(performer);
				}
			}

			// Create brain if there isn't one.
			if (Brain == null)
			{
				Brain = new Brain(DependencyManager, callbackService, state, brainGraphs);
			}
		}

		protected void OnDestroy()
		{
			((Actor)Actor).Dispose();
		}
	}
}
