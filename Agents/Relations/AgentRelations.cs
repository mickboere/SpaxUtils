using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentRelations : IRelations
	{
		public const float THRESHOLD = 0.2f;
		public const float MIN = -1f;
		public const float MAX = 1f;

		/// <inheritdoc/>
		public IReadOnlyDictionary<string, float> Relations => relations;
		/// <inheritdoc/>
		public IReadOnlyCollection<string> Enemies => enemies;
		/// <inheritdoc/>
		public IReadOnlyCollection<string> Friends => friends;

		private Dictionary<string, float> relations;
		private List<string> enemies;
		private List<string> friends;

		public AgentRelations(RuntimeDataCollection relationData)
		{
			relations = new Dictionary<string, float>();
			foreach (RuntimeDataEntry data in relationData.Data)
			{
				relations.Add(data.ID, (float)data.Value);
			}
			Update();
		}

		/// <inheritdoc/>
		public void Set(string relation, float amount)
		{
			if (!relations.ContainsKey(relation))
			{
				relations.Add(relation, 0f);
			}

			relations[relation] = Mathf.Clamp(amount, MIN, MAX);
			Update();
		}

		/// <inheritdoc/>
		public void Adjust(string relation, float amount)
		{
			if (!relations.ContainsKey(relation))
			{
				relations.Add(relation, 0f);
			}

			relations[relation] = Mathf.Clamp(relations[relation] + amount, -1f, 1f);
			Update();
		}

		/// <inheritdoc/>
		public float Score(IIdentification id)
		{
			float score = 0f;
			if (relations.ContainsKey(id.ID))
			{
				score += relations[id.ID];
			}
			foreach (string label in id.Labels)
			{
				if (relations.ContainsKey(label))
				{
					score += relations[label];
				}
			}
			return score;
		}

		private void Update()
		{
			enemies = relations.Where(r => r.Value < -THRESHOLD).Select(s => s.Key).ToList();
			friends = relations.Where(r => r.Value > THRESHOLD).Select(s => s.Key).ToList();
		}
	}
}
