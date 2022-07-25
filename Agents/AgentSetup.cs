using SpaxUtils.StateMachine;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Standard <see cref="IAgentSetup"/> implementation unique per <see cref="IAgent"/>.
	/// </summary>
	public class AgentSetup : IAgentSetup
	{
		/// <inheritdoc/>
		public IIdentification Identification { get; set; }

		/// <inheritdoc/>
		public Agent Frame { get; set; }

		/// <inheritdoc/>
		public StateMachineGraph Brain { get; set; }

		/// <inheritdoc/>
		public AgentBodyComponent Body { get; set; }

		/// <inheritdoc/>
		public IList<GameObject> Children { get; set; }

		/// <inheritdoc/>
		public IList<object> Dependencies { get; set; }

		public AgentSetup(
			IIdentification identification,
			Agent frame,
			StateMachineGraph brain,
			AgentBodyComponent body,
			IList<GameObject> children,
			IList<object> dependencies)
		{
			Identification = identification;
			Frame = frame;
			Brain = brain;
			Body = body;
			Children = new List<GameObject>(children);
			Dependencies = new List<object>(dependencies);
		}

		public AgentSetup(IAgentSetup template)
		{
			Identification = template.Identification;
			Frame = template.Frame;
			Brain = template.Brain;
			Body = template.Body;
			Children = new List<GameObject>(template.Children);
			Dependencies = new List<object>(template.Dependencies);
		}
	}
}
