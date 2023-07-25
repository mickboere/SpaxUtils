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
		public float DefaultMass => defaultMass;

		public bool HasAnimator => AnimatorWrapper != null && AnimatorWrapper.Animator != null;
		public AnimatorWrapper AnimatorWrapper => RefComponentRelative(ref animatorWrapper);

		public SkinnedMeshRenderer ReferenceMesh => RefComponentRelative(ref referenceSkin);
		public Transform SkeletonRootBone => skeletonRootBone;
		public Transform Head => head;
		public IReadOnlyList<Transform> Skeleton => GetSkeleton();
		public Vector3 Center => Skeleton.GetCenter(t => t.TryGetComponent(out SkeletonBoneOptions options) ? options.Weight : 1f);

		[SerializeField] private float scale = 1f;
		[SerializeField] private RigidbodyWrapper rigidbodyWrapper;
		[SerializeField] private float defaultMass;
		[SerializeField] private AnimatorWrapper animatorWrapper;
		[SerializeField] private Transform skeletonRootBone;
		[SerializeField] private Transform head;
		[SerializeField] private SkinnedMeshRenderer referenceSkin;
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
		}

		protected void OnValidate()
		{
			EnsureAllComponents();
		}

		protected void Reset()
		{
			EnsureAllComponents();
		}

		public List<Transform> GetSkeleton(bool refresh = false)
		{
			if (skeleton == null || refresh)
			{
				skeleton = SkeletonRootBone.CollectChildrenRecursive((t) => !t.TryGetComponent(out IExcludeFromSkeleton ex) || ex.Exclude);
			}
			return skeleton;
		}

		private void EnsureAllComponents()
		{
			RefComponentRelative(ref rigidbodyWrapper);
			RefComponentRelative(ref animatorWrapper);
			RefComponentRelative(ref targetableComponent);
			RefComponentRelative(ref referenceSkin);
		}

		private T RefComponentRelative<T>(ref T component)
		{
			component = gameObject.GetComponentRelative<T>();
			return component;
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
