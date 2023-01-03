using UnityEngine;

namespace SpaxUtils
{
	public class AgentHitHandler : EntityComponentBase
	{
		[SerializeField] private PoseSequenceBlendTree hitBlendTree;
		[SerializeField] private float maxHitDispersionTime = 3f;

		private IHittable hittable;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser animatorPoser;

		public void InjectDependencies(IHittable hittable, RigidbodyWrapper rigidbodyWrapper, AnimatorPoser animatorPoser)
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
			//float dispersionTime = hitData.Mass / rigidbodyWrapper.Mass;
			//Vector3 momentum = hitData.Momentum * (dispersionTime / maxHitDispersionTime);
			rigidbodyWrapper.AddImpact(hitData.Momentum, hitData.Mass);
		}
	}
}
