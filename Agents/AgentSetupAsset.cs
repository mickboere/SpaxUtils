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
		public IList<GameObject> Children =>
			template != null && template.Children != null ?
				template.Children.Union(children).ToList() :
				children;

		/// <inheritdoc/>
		public IList<object> Dependencies =>
			template != null && template.Dependencies != null ?
				template.Dependencies.Union(dependencies).ToList() :
				dependencies.Cast<object>().ToList();

		/// <inheritdoc/>
		public RuntimeDataCollection Data =>
			template != null && template.Data != null ? data.ApplyToRuntimeDataCollection(template.Data) :
			data.Count > 0 ? data.ToRuntimeDataCollection(identification.ID) :
			null;

		[SerializeField] private AgentSetupAsset template;
		[SerializeField] private Identification identification;
		[SerializeField] private Agent frame;
		[SerializeField] private AgentBodyComponent body;
		[SerializeField] private List<GameObject> children;
		[SerializeField] private List<ScriptableObject> dependencies;
		[SerializeField] private LabeledDataCollection data;
	}
}
