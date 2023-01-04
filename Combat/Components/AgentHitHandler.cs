using UnityEngine;

namespace SpaxUtils
{
	public class AgentHitHandler : EntityComponentBase
	{
		[SerializeField] private PoseSequenceBlendTree hitBlendTree;
		[SerializeField] private float maxStunTime = 3f;

		private IHittable hittable;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser animatorPoser;

		private HitData lastHit;
		private Timer stunTimer;
		private FloatOperationModifier stunControlMod;

		public void InjectDependencies(IHittable hittable, RigidbodyWrapper rigidbodyWrapper, AnimatorPoser animatorPoser)
		{
			this.hittable = hittable;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.animatorPoser = animatorPoser;
		}

		protected void OnEnable()
		{
			hittable.OnHitEvent += OnHitEvent;
			stunControlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, stunControlMod);
		}

		protected void OnDisable()
		{
			hittable.OnHitEvent -= OnHitEvent;
			rigidbodyWrapper.Control.RemoveModifier(this);
			stunControlMod.Dispose();
		}

		protected void Update()
		{
			if (stunTimer)
			{
				PoserStruct instructions = hitBlendTree.GetInstructions(-lastHit.Direction.Localize(rigidbodyWrapper.transform).normalized, 0f);
				animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, instructions, 5, stunTimer.Progress.ReverseInOutCubic());
				stunControlMod.SetValue(stunTimer.Progress.InOutCubic());
			}
		}

		private void OnHitEvent(HitData hitData)
		{
			lastHit = hitData;
			rigidbodyWrapper.AddImpact(hitData.Velocity, hitData.Mass);

			float stunTime = hitData.Mass / rigidbodyWrapper.Mass;
			stunTimer = new Timer(Mathf.Min(stunTime, maxStunTime));

			SpaxDebug.Log($"OnHitEvent", $"v={hitData.Velocity}, m={hitData.Mass}, stun={stunTime}s");
		}
	}
}
