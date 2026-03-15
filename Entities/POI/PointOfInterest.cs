//using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// A point of interest that wandering agents can navigate to and perform an action at.
	/// Only one agent may occupy a POI at a time.
	/// </summary>
	public class PointOfInterest : MonoBehaviour
	{
		/// <summary>Whether an agent is currently occupying this POI.</summary>
		public bool IsOccupied => occupant != null;

		/// <summary>The action key passed to the agent's Actor when they arrive. Stub for AnimatedBehaviour system.</summary>
		public string ActionKey => actionKey;

		/// <summary>Tags used by WanderNode to filter which POIs this agent type will visit.</summary>
		public string[] Tags => tags;

		[SerializeField, ConstDropdown(typeof(IPOITags))] private string[] tags;
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

		/// <summary>
		/// Returns whether this POI has all of the requested tags.
		/// Passing null or empty matches any POI.
		/// </summary>
		public bool HasTags(string[] required)
		{
			if (required == null || required.Length == 0)
				return true;

			foreach (string req in required)
			{
				bool found = false;
				foreach (string tag in tags)
				{
					if (tag == req) { found = true; break; }
				}
				if (!found) return false;
			}

			return true;
		}

		public string GetString()
		{
			return $"POI \"{gameObject.name}\", tags=[{tags.Join()}], actionKey={actionKey}, occupied={IsOccupied}, occupant={occupant.ID}";
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
