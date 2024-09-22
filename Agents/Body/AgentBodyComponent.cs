using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentBodyComponent : EntityComponentBase, IAgentBody
	{
		/// <inheritdoc/>
		public float Scale => scale;

		public bool HasRigidbody => RigidbodyWrapper != null;
		public RigidbodyWrapper RigidbodyWrapper => RefComponentRelative(ref rigidbodyWrapper);
		public CapsuleCollider Bumper => bumper;
		public float BaseMass => baseMass;
		public float BaseSpeed => baseSpeed;

		public bool HasAnimator => AnimatorWrapper != null && AnimatorWrapper.Animator != null;
		public AnimatorWrapper AnimatorWrapper => RefComponentRelative(ref animatorWrapper);

		public Transform SkeletonRootBone => skeletonRootBone;
		public IReadOnlyList<Transform> Skeleton => GetSkeleton();
		public IReadOnlyList<Renderer> Renderers => renderers;

		public Vector3 Center => Skeleton.GetCenter(t => t.TryGetComponent(out SkeletonBoneOptions options) ? options.Weight : 1f);

		public Transform Head => head;

		[SerializeField] private float scale = 1f;
		[SerializeField] private RigidbodyWrapper rigidbodyWrapper;
		[SerializeField] private CapsuleCollider bumper;
		[SerializeField] private float baseMass = 100f;
		[SerializeField] private float baseSpeed = 4f;
		[SerializeField] private AnimatorWrapper animatorWrapper;
		[SerializeField] private Transform skeletonRootBone;
		[SerializeField] private Transform head;
		[SerializeField] private List<Renderer> renderers;
		[SerializeField] private bool debug;

		private ITargetable targetableComponent;
		private List<Transform> skeleton;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, AnimatorWrapper animatorWrapper, ITargetable targetableComponent)
		{
			this.rigidbodyWrapper = this.rigidbodyWrapper ?? rigidbodyWrapper;
			this.animatorWrapper = this.animatorWrapper ?? animatorWrapper;
			this.targetableComponent = targetableComponent;
		}

		protected void Awake()
		{
			GetSkeleton();

			// Apply base mass.
			if (HasRigidbody && Entity.TryGetStat(AgentStatIdentifiers.MASS, out EntityStat mass))
			{
				mass.BaseValue = BaseMass;
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

		protected void OnDrawGizmos()
		{
			if (!debug)
			{
				return;
			}

			Gizmos.color = Color.magenta;
			Gizmos.DrawSphere(Center, 0.05f);
		}
	}
}
