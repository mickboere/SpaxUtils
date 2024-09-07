using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "AgentRelationData", menuName = "ScriptableObjects/AgentRelationData")]
	public class AgentRelationData : ScriptableObject, IRelationData, IBindingKeyProvider
	{
		[Serializable]
		public class LabelRelation
		{
			public string Label => label;
			public float Relation => relation;

			[SerializeField, ConstDropdown(typeof(IIdentificationLabels))] private string label;
			[SerializeField, Range(-1f, 1f)] private float relation;
		}

		public object BindingKey => GetInstanceID();

		[SerializeField] private List<LabelRelation> labelRelations;

		public IReadOnlyDictionary<string, float> GetRelations() => labelRelations.ToDictionary(k => k.Label, v => v.Relation);
	}
}
