using UnityEngine;

namespace SpaxUtils
{
	public class TargetingProjectile : BaseProjectile
	{
		[Header("Targeting")]
		[SerializeField] private float rotateSpeed = 5f;

		protected override void OnUpdate(float delta)
		{
			if (target != null)
			{
				transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(target.Center - transform.position), rotateSpeed * delta);
			}

			transform.position += Velocity * delta;
		}
	}
}
