using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentBodyComponent : EntityComponentBase, IAgentBody
	{
		/// <inheritdoc/>
		public float Scale => scale;

		public bool HasRigidbody => RigidbodyWrapper != null && RigidbodyWrapper.Rigidbody != null;
		public bool HasAnimator => AnimatorWrapper != null && AnimatorWrapper.Animator != null;

		public RigidbodyWrapper RigidbodyWrapper => Pray(ref rigidbodyWrapper);
		public AnimatorWrapper AnimatorWrapper => Pray(ref animatorWrapper);
		public SkinnedMeshRenderer ReferenceMesh => Pray(ref referenceSkin);
		public Transform SkeletonRootBone => skeletonRootBone;
		public IReadOnlyList<Transform> Skeleton => GetSkeleton();
		public Vector3 Center => Skeleton.GetCenter(t => t.TryGetComponent(out SkeletonBoneOptions options) ? options.Weight : 1f);

		[SerializeField] private float scale = 1f;
		[SerializeField] private RigidbodyWrapper rigidbodyWrapper;
		[SerializeField] private AnimatorWrapper animatorWrapper;
		[SerializeField] private Transform skeletonRootBone;
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
			Pray(ref rigidbodyWrapper);
			Pray(ref animatorWrapper);
			Pray(ref targetableComponent);
			Pray(ref referenceSkin);
		}

		private T Pray<T>(ref T component)
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
