using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentBodyComponent : AgentComponentBase, IAgentBody
	{
		public float BaseMass => baseMass;
		public Vector3 BaseSize => baseSize;
		public float Scale => scale;

		public RigidbodyWrapper RigidbodyWrapper => RefComponentRelative(ref rigidbodyWrapper);
		public bool HasRigidbody => RigidbodyWrapper != null;
		public CapsuleCollider Bumper => bumper;
		public Transform SkeletonRootBone => skeletonRootBone;
		public IReadOnlyList<Transform> Skeleton => GetSkeleton();
		public IReadOnlyList<Renderer> Renderers => renderers;
		public Vector3 Center => SkeletonRootBone == null ? transform.position : SkeletonRootBone.position;

		public Transform Head => head;

		[Header("Base Values")]
		[SerializeField] private float baseMass = 100f;
		[SerializeField] private Vector3 baseSize = new Vector3(0.5f, 1.8f, 0.5f);
		[Header("Active Values")]
		[SerializeField] private float scale = 1f;
		[Header("References")]
		[SerializeField] private RigidbodyWrapper rigidbodyWrapper;
		[SerializeField] private CapsuleCollider bumper;
		[SerializeField] private Transform skeletonRootBone;
		[SerializeField] private List<Renderer> renderers;
		[SerializeField] private Transform head;
		[SerializeField] private bool scaleHead;

		private ITargetable targetableComponent;
		private RuntimeDataCollection runtimeData;

		private List<Transform> _skeleton;
		private Dictionary<Transform, SkeletonBoneOptions> _boneOptions;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, AnimatorWrapper animatorWrapper, ITargetable targetableComponent,
			[Optional] RuntimeDataCollection runtimeData)
		{
			this.rigidbodyWrapper = this.rigidbodyWrapper ?? rigidbodyWrapper;
			this.targetableComponent = targetableComponent;
			this.runtimeData = runtimeData;
		}

		protected void OnEnable()
		{
			GetSkeleton();

			// Apply base mass.
			if (HasRigidbody && Entity.Stats.TryGetStat(AgentStatIdentifiers.MASS, out EntityStat mass))
			{
				mass.BaseValue = BaseMass;
			}

			// Check if height or scale data has been supplied through runtime data.
			if (runtimeData != null)
			{
				if (runtimeData.TryGetValue(EntityDataIdentifiers.HEIGHT, out float height))
				{
					scale = height / baseSize.y;
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
				head.localScale = Vector3.one / Scale.Min(1.1f) * Mathf.Lerp(1f, 0.5f, Scale.InvertClamped());
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
			RefComponentRelative(ref targetableComponent);
		}

		private T RefComponentRelative<T>(ref T component)
		{
			component = component ?? gameObject.GetComponentRelative<T>();
			return component;
		}

		private List<Transform> GetSkeleton(bool refresh = false)
		{
			if (_skeleton == null || refresh)
			{
				_skeleton = SkeletonRootBone.CollectChildrenRecursive((t) => !t.TryGetComponent(out IExcludeFromSkeleton ex) || ex.Exclude);
				_boneOptions = new Dictionary<Transform, SkeletonBoneOptions>();
				foreach (Transform bone in _skeleton)
				{
					if (bone.TryGetComponent(out SkeletonBoneOptions options))
					{
						_boneOptions.Add(bone, options);
					}
				}
			}
			return _skeleton;
		}

		protected void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.magenta;
			Gizmos.DrawSphere(Center, 0.05f * Scale);
			Gizmos.color = Color.white;
			Gizmos.DrawWireCube(transform.position + Vector3.up * BaseSize.y * 0.5f * Scale, BaseSize * Scale);
		}
	}
}
