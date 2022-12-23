using UnityEngine;

namespace SpaxUtils
{
	public class AgentHitHandler : EntityComponentBase
	{
		[SerializeField] private PoseSequenceBlendTree hitBlendTree;
		[SerializeField] private float maxHitDispersionTime = 3f;

		private IHittable hittable;
		private RigidbodyWrapper rigidbodyWrapper;

		public void InjectDependencies(IHittable hittable, RigidbodyWrapper rigidbodyWrapper)
		{
			this.hittable = hittable;
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		protected void OnEnable()
		{
			hittable.OnHitEvent += OnHitEvent;
		}

		protected void OnDisable()
		{
			hittable.OnHitEvent -= OnHitEvent;
		}

		private void OnHitEvent(HitData hitData)
		{
			float dispersionTime = hitData.Impact.Force / rigidbodyWrapper.Mass;
			Vector3 momentum = hitData.Impact.Momentum * (dispersionTime / maxHitDispersionTime);
			rigidbodyWrapper.AddImpact(new Impact(momentum, hitData.Impact.Force));
		}
	}
}
