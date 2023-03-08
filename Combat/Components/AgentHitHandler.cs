using UnityEngine;

namespace SpaxUtils
{
	public class AgentHitHandler : EntityComponentBase
	{
		[SerializeField] private PoseSequenceBlendTree hitBlendTree;
		[SerializeField] private float maxStunTime = 3f;

		private IAgent agent;
		private IHittable hittable;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser animatorPoser;

		private HitData lastHit;
		private Timer stunTimer;
		private FloatOperationModifier stunControlMod;

		public void InjectDependencies(IAgent agent, IHittable hittable, RigidbodyWrapper rigidbodyWrapper, AnimatorPoser animatorPoser)
		{
			this.agent = agent;
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
			rigidbodyWrapper.AddImpact(hitData.Inertia, hitData.Force);

			float stunTime = hitData.Force / rigidbodyWrapper.Mass;
			stunTimer = new Timer(Mathf.Min(stunTime, maxStunTime));

			foreach (var item in hitData.Damages)
			{
				EntityStat stat = Entity.GetStat(item.Key);
				if (stat != null)
				{
					stat.BaseValue -= item.Value;
				}
			}

			SpaxDebug.Log($"OnHitEvent", $"i={hitData.Inertia}, f={hitData.Force}, stun={stunTime}s");
		}
	}
}
