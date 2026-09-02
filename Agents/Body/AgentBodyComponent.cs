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
		/// <summary>Every collider on the body itself. Sheathes, equipment and their children are excluded.</summary>
		public IReadOnlyList<Collider> BodyColliders { get { GetSkeleton(); return _colliders; } }
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

		private TransformLookup lookup;

		private List<Transform> _skeleton;
		private Dictionary<Transform, SkeletonBoneOptions> _boneOptions;
		private List<Collider> _colliders;
		private Dictionary<Transform, List<Collider>> _boneColliders;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, AnimatorWrapper animatorWrapper, ITargetable targetableComponent,
			[Optional] RuntimeDataCollection runtimeData)
		{
			this.rigidbodyWrapper = this.rigidbodyWrapper ?? rigidbodyWrapper;
			this.targetableComponent = targetableComponent;
			this.runtimeData = runtimeData;
		}

		protected void OnEnable()
		{
			// Dropped rather than rebuilt: the walk is only paid for if something actually reads it, and
			// domain reload being off means a cache from a previous session would otherwise survive.
			RefreshSkeleton();

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
			RefreshSkeleton();
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

		/// <summary>
		/// The colliders on <paramref name="boneID"/>'s own bone, by <see cref="HumanBoneIdentifiers"/> or name.
		/// </summary>
		public bool TryGetBoneColliders(string boneID, out IReadOnlyList<Collider> colliders)
		{
			GetSkeleton();
			if (lookup == null)
			{
				lookup = gameObject.GetComponentRelative<TransformLookup>();
			}

			Transform bone = lookup == null ? null : lookup.Lookup(boneID);
			if (bone != null && _boneColliders != null && _boneColliders.TryGetValue(bone, out List<Collider> found))
			{
				colliders = found;
				return true;
			}

			colliders = null;
			return false;
		}

		/// <summary>
		/// Drops the cached skeleton, so the next read walks the rig again. Only needed when bones are
		/// added or removed at runtime — equipment coming and going never enters it.
		/// </summary>
		public void RefreshSkeleton()
		{
			_skeleton = null;
		}

		/// <summary>
		/// Walks the rig once and caches it. Anything marked <see cref="IExcludeFromSkeleton"/> is dropped
		/// along with everything under it, which is what keeps sheathes and equipment out.
		/// </summary>
		private List<Transform> GetSkeleton(bool refresh = false)
		{
			if ((_skeleton != null && !refresh) || SkeletonRootBone == null)
			{
				return _skeleton;
			}

			// The root bone carries colliders of its own, so it belongs in the walk with the rest.
			_skeleton = SkeletonRootBone.CollectChildrenRecursive(IsBone, includeParent: true);
			_boneOptions = new Dictionary<Transform, SkeletonBoneOptions>();
			_boneColliders = new Dictionary<Transform, List<Collider>>();
			_colliders = new List<Collider>();

			List<Collider> bodyColliders = new List<Collider>();
			foreach (Transform bone in _skeleton)
			{
				if (bone.TryGetComponent(out SkeletonBoneOptions options))
				{
					_boneOptions.Add(bone, options);
				}

				bone.GetComponents(bodyColliders);
				if (bodyColliders.Count > 0)
				{
					_boneColliders.Add(bone, new List<Collider>(bodyColliders));
					_colliders.AddRange(bodyColliders);
				}
			}

			return _skeleton;
		}

		/// <summary>Whether a transform is part of the body, as opposed to a mount or what is hanging off it.</summary>
		private static bool IsBone(Transform transform)
		{
			return !transform.TryGetComponent(out IExcludeFromSkeleton exclude) || !exclude.Exclude;
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
