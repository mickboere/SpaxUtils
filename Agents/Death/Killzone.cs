using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// World trigger that instantly kills any <see cref="IAgent"/> entering its bounds.
	/// Requires a Collider on this GameObject with isTrigger enabled.
	/// </summary>
	[RequireComponent(typeof(Collider))]
	public class KillZone : MonoBehaviour
	{
		[SerializeField, Tooltip("Death cause string passed to the DeathContext.")]
		private string cause = "KillZone";

		private void OnTriggerEnter(Collider other)
		{
			IAgent agent = other.GetComponentInParent<IAgent>();
			if (agent != null && agent.Alive)
			{
				agent.Die(new DeathContext(agent, null, cause));
			}
		}
	}
}
