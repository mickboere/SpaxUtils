using UnityEngine;

namespace SpaxUtils
{
	public class WeaponComponent : MonoBehaviour
	{
		[field: SerializeField] public Transform Base { get; private set; }
		[field: SerializeField] public Transform Tip { get; private set; }

		protected void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.red;
			Gizmos.DrawLine(Base.position, Tip.position);
		}
	}
}
