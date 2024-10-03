using SpaxUtils.StateMachines;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "AgentSetupAsset", menuName = "ScriptableObjects/Agent Setup Asset")]
	public class AgentSetupAsset : ScriptableObject, IAgentSetup
	{
		/// <inheritdoc/>
		public IIdentification Identification => identification;

		/// <inheritdoc/>
		public Agent Frame =>
			frame == null && template != null ?
				template.Frame :
				frame;

		/// <inheritdoc/>
		public AgentBodyComponent Body =>
			body == null && template != null ?
				template.Body :
				body;

		/// <inheritdoc/>
		public IList<StateMachineGraph> BrainGraphs =>
			template != null && template.BrainGraphs != null ?
				new List<StateMachineGraph>().Concat(template.BrainGraphs).Concat(brainGraphs).ToList() :
				brainGraphs;

		/// <inheritdoc/>
		public IList<GameObject> Children =>
			template != null && template.Children != null ?
				new List<GameObject>().Concat(template.Children).Concat(children).ToList() :
				children;

		/// <inheritdoc/>
		public IList<object> Dependencies =>
			template != null && template.Dependencies != null ?
				new List<object>().Concat(template.Dependencies).Concat(dependencies).ToList() :
				new List<object>().Concat(dependencies).ToList();

		/// <inheritdoc/>
		public RuntimeDataCollection Data =>
			template != null && template.Data != null ? data.ApplyToRuntimeDataCollection(template.Data) :
			data.Count > 0 ? data.ToRuntimeDataCollection(identification.ID) :
			null;

		[SerializeField] private AgentSetupAsset template;
		[SerializeField] private Identification identification;
		[SerializeField] private Agent frame;
		[SerializeField] private AgentBodyComponent body;
		[SerializeField] private List<StateMachineGraph> brainGraphs;
		[SerializeField] private List<GameObject> children;
		[SerializeField] private List<ScriptableObject> dependencies;
		[SerializeField] private LabeledDataCollection data;
	}
}
