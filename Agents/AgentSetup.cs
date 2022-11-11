using SpaxUtils.StateMachine;
using System.Collections.Generic;
using System.Linq;
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

		public AgentSetup(
			IAgentSetup template,
			IIdentification identification = null,
			Agent frame = null,
			StateMachineGraph brain = null,
			AgentBodyComponent body = null,
			IList<GameObject> children = null,
			IList<object> dependencies = null)
		{
			Identification = identification ?? template.Identification;
			Frame = frame ?? template.Frame;
			Brain = brain ?? template.Brain;
			Body = body ?? template.Body;
			Children = children == null ? new List<GameObject>(template.Children) : new List<GameObject>().Concat(template.Children).Concat(children).ToList();
			Dependencies = dependencies == null ? new List<object>(template.Dependencies) : new List<object>().Concat(template.Dependencies).Concat(dependencies).ToList();
		}
	}
}
