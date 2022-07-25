using SpaxUtils.StateMachine;
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
		public StateMachineGraph Brain =>
			brain == null && template != null ?
				template.Brain :
				brain;

		/// <inheritdoc/>
		public AgentBodyComponent Body =>
			body == null && template != null ?
				template.Body :
				body;

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

		[SerializeField] private AgentSetupAsset template;
		[SerializeField] private Identification identification;
		[SerializeField] private Agent frame;
		[SerializeField] private StateMachineGraph brain;
		[SerializeField] private AgentBodyComponent body;
		[SerializeField] private List<GameObject> children;
		[SerializeField] private List<AgentDependencyAssetBase> dependencies;
	}
}
