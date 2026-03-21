using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// A point of interest that wandering agents can navigate to and perform an action at.
	/// Only one agent may occupy a POI at a time.
	/// POI filtering uses <see cref="IIdentification.Labels"/> on the parent <see cref="Entity"/>
	/// instead of dedicated tags.
	/// </summary>
	[RequireComponent(typeof(Entity))]
	public class PointOfInterest : EntityComponentMono
	{
		/// <summary>Whether an agent is currently occupying this POI.</summary>
		public bool IsOccupied => occupant != null;

		/// <summary>The action key passed to the agent's Actor when they arrive. Stub for AnimatedBehaviour system.</summary>
		public string ActionKey => actionKey;

		[SerializeField, ConstDropdown(typeof(IActIdentifiers), true)] private string actionKey;
		[SerializeField] private float dwellTimeMin = 5f;
		[SerializeField] private float dwellTimeMax = 15f;

		private IAgent occupant;

		/// <summary>
		/// Attempts to occupy this POI. Returns false if already occupied.
		/// </summary>
		public bool TryOccupy(IAgent agent)
		{
			if (IsOccupied)
			{
				return false;
			}

			occupant = agent;
			return true;
		}

		/// <summary>
		/// Vacates this POI. Only the current occupant may vacate.
		/// </summary>
		public void Vacate(IAgent agent)
		{
			if (occupant == agent)
			{
				occupant = null;
			}
		}

		/// <summary>
		/// Returns a random dwell time within the configured range.
		/// </summary>
		public float SampleDwellTime()
		{
			return Random.Range(dwellTimeMin, dwellTimeMax);
		}

		public string GetString()
		{
			return $"POI \"{Entity.ID}\", labels=[{string.Join(", ", Entity.Identification.Labels)}], actionKey={actionKey}, occupied={IsOccupied}, occupant={(occupant != null ? occupant.ID : "none")}";
		}

		protected virtual void OnDrawGizmos()
		{
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = Color.red;
			Gizmos.DrawSphere(Vector3.zero, 0.1f);
			Gizmos.color = Color.blue;
			Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.5f);
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
			Gizmos.color = Color.blue;
			Gizmos.DrawCube(Vector3.forward * 0.5f, Vector3.one * 0.1f);
		}
	}
}
