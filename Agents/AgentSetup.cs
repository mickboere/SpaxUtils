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
		public bool ContainsData => data != null;

		private RuntimeDataCollection data;

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
			this.data = data;
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

			if (template.ContainsData)
			{
				this.data = template.RetrieveDataClone();
				this.data.ID = Identification.ID;
			}
			else
			{
				this.data = new RuntimeDataCollection(Identification.ID);
			}

			if (data != null)
			{
				this.data.AppendCollection(data, true);
			}
		}

		/// <inheritdoc/>
		public RuntimeDataCollection RetrieveDataClone()
		{
			return data.CloneCollection();
		}
	}
}
