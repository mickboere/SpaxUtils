using SpaxUtils.StateMachines;
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
		public AgentBodyComponent Body { get; set; }

		/// <inheritdoc/>
		public IList<GameObject> Children { get; set; }

		/// <inheritdoc/>
		public IList<object> Dependencies { get; set; }

		/// <inheritdoc/>
		public RuntimeDataCollection Data { get; set; }

		public AgentSetup(
			IIdentification identification,
			Agent frame,
			AgentBodyComponent body,
			IList<GameObject> children,
			IList<object> dependencies,
			RuntimeDataCollection data)
		{
			Identification = identification;
			Frame = frame;
			Body = body;
			Children = new List<GameObject>(children);
			Dependencies = new List<object>(dependencies);
			Data = data;
		}

		public AgentSetup(
			IAgentSetup template,
			IIdentification identification = null,
			Agent frame = null,
			AgentBodyComponent body = null,
			IList<StateMachineGraph> brainGraphs = null,
			IList<GameObject> children = null,
			IList<object> dependencies = null,
			RuntimeDataCollection data = null)
		{
			Identification = identification ?? template.Identification;
			Frame = frame ?? template.Frame;
			Body = body ?? template.Body;
			Children = children == null ? new List<GameObject>(template.Children) : template.Children.Union(children).ToList();
			Dependencies = dependencies == null ? new List<object>(template.Dependencies) : template.Dependencies.Union(dependencies).ToList();
			Data = template != null && template.Data != null ? template.Data.Append(data) : data;
		}
	}
}
