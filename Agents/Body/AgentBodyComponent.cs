using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentBodyComponent : AgentComponentBase, IAgentBody
	{
		public float BaseMass => baseMass;
		public float BaseSpeed => baseSpeed;
		public float Scale => scale;

		public RigidbodyWrapper RigidbodyWrapper => RefComponentRelative(ref rigidbodyWrapper);
		public CapsuleCollider Bumper => bumper;
		public AnimatorWrapper AnimatorWrapper => RefComponentRelative(ref animatorWrapper);
		public Transform SkeletonRootBone => skeletonRootBone;
		public IReadOnlyList<Transform> Skeleton => GetSkeleton();
		public IReadOnlyList<Renderer> Renderers => renderers;

		public bool HasRigidbody => RigidbodyWrapper != null;
		public bool HasAnimator => AnimatorWrapper != null && AnimatorWrapper.Animator != null;

		public Vector3 Center => Skeleton.GetCenter(t => t.TryGetComponent(out SkeletonBoneOptions options) ? options.Weight : 1f);

		public Transform Head => head;

		[Header("Base Values")]
		[SerializeField] private float baseMass = 100f;
		[SerializeField] private float baseSpeed = 4f;
		[SerializeField] private float baseHeight = 1.8f;
		[SerializeField] private float scale = 1f;
		[SerializeField] private bool scaleHead;
		[Header("References")]
		[SerializeField] private RigidbodyWrapper rigidbodyWrapper;
		[SerializeField] private CapsuleCollider bumper;
		[SerializeField] private AnimatorWrapper animatorWrapper;
		[SerializeField] private Transform skeletonRootBone;
		[SerializeField] private Transform head;
		[SerializeField] private List<Renderer> renderers;

		private ITargetable targetableComponent;
		private RuntimeDataCollection runtimeData;

		private List<Transform> skeleton;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, AnimatorWrapper animatorWrapper, ITargetable targetableComponent,
			[Optional] RuntimeDataCollection runtimeData)
		{
			this.rigidbodyWrapper = this.rigidbodyWrapper ?? rigidbodyWrapper;
			this.animatorWrapper = this.animatorWrapper ?? animatorWrapper;
			this.targetableComponent = targetableComponent;
			this.runtimeData = runtimeData;
		}

		protected void OnEnable()
		{
			GetSkeleton();

			// Apply base mass.
			if (HasRigidbody && Entity.TryGetStat(AgentStatIdentifiers.MASS, out EntityStat mass))
			{
				mass.BaseValue = BaseMass;
			}

			// Check if height or scale data has been supplied through runtime data.
			if (runtimeData != null)
			{
				if (runtimeData.TryGetValue(EntityDataIdentifiers.HEIGHT, out float height))
				{
					scale = height / baseHeight;
				}
				else if (runtimeData.TryGetValue(EntityDataIdentifiers.SCALE, out float scale))
				{
					this.scale = scale;
				}
			}

			// Apply scale.
			transform.localScale = Vector3.one * Scale;
			if (scaleHead)
			{
				// Scale head size to compensate for body scale.
				head.localScale = Vector3.one / Scale * Mathf.Lerp(1f, 0.5f, Scale.InvertClamped());
			}
		}

		protected void OnValidate()
		{
			EnsureAllComponents();
		}

		protected void Reset()
		{
			EnsureAllComponents();
		}

		private void EnsureAllComponents()
		{
			RefComponentRelative(ref rigidbodyWrapper);
			RefComponentRelative(ref animatorWrapper);
			RefComponentRelative(ref targetableComponent);
		}

		private T RefComponentRelative<T>(ref T component)
		{
			component = component ?? gameObject.GetComponentRelative<T>();
			return component;
		}

		private List<Transform> GetSkeleton(bool refresh = false)
		{
			if (skeleton == null || refresh)
			{
				skeleton = SkeletonRootBone.CollectChildrenRecursive((t) => !t.TryGetComponent(out IExcludeFromSkeleton ex) || ex.Exclude);
			}
			return skeleton;
		}

		protected void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.magenta;
			Gizmos.DrawSphere(Center, 0.05f * Scale);
		}
	}
}
