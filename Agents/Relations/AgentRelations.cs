using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentRelations : IRelations
	{
		/// <summary>
		/// The threshold above which is considered a friend, and below its negative considered an enemy.
		/// </summary>
		public const float THRESHOLD = 0.2f;

		/// <inheritdoc/>
		public event Action RelationsUpdatedEvent;

		/// <inheritdoc/>
		public event Action<string> RelationUpdatedEvent;

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

			float previous = relations[relation];
			relations[relation] = amount;
			if (!relations[relation].Approx(previous))
			{
				Update();
				RelationUpdatedEvent?.Invoke(relation);
			}
		}

		/// <inheritdoc/>
		public void Adjust(string relation, float amount)
		{
			if (!relations.ContainsKey(relation))
			{
				relations.Add(relation, 0f);
			}

			float previous = relations[relation];
			relations[relation] = relations[relation] + amount;
			if (!relations[relation].Approx(previous))
			{
				Update();
				RelationUpdatedEvent?.Invoke(relation);
			}
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
			RelationsUpdatedEvent?.Invoke();
		}
	}
}
