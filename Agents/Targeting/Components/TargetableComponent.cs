using UnityEngine;
using SpaxUtils;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="ITargetable"/> <see cref="IEntityComponent"/> implementation.
	/// Provides basic data required for targetting an entity.
	/// </summary>
	public class TargetableComponent : EntityComponentMono, ITargetable
	{
		/// <inheritdoc/>
		public Vector3 Point => headProvider != null && headProvider.Head != null ? headProvider.Head.position : Center;

		/// <inheritdoc/>
		public Vector3 Orientation => headProvider != null && headProvider.Head != null ? headProvider.Head.forward : transform.forward;

		/// <inheritdoc/>
		public virtual Vector3 Position => transform.position;

		/// <inheritdoc/>
		public virtual Quaternion Rotation => transform.rotation;

		/// <inheritdoc/>
		public virtual Bounds Bounds
		{
			get
			{
				GetRenderers();
				if (renderersAtStart == null || renderersAtStart.Length == 0)
				{
					return new Bounds(transform.position, Vector3.one);
				}
				Bounds bounds = new Bounds(renderersAtStart[0].bounds.center, renderersAtStart[0].bounds.size);
				foreach (Renderer renderer in renderersAtStart)
				{
					bounds.Encapsulate(renderer.bounds);
				}

				return bounds;
			}
		}

		/// <inheritdoc/>
		public Vector3 Center => Bounds.center;

		/// <inheritdoc/>
		public float Radius => radius.Approx(0f) ? Mathf.Max(Size.x, Size.z) * 0.5f : radius;

		/// <inheritdoc/>
		public Vector3 Size => Application.isPlaying && useSizeAtStart ? startingSize :
			size == Vector3.zero ? Bounds.size :
			size;

		/// <inheritdoc/>
		public bool IsTargetable { get; private set; } = true;

		[SerializeField] private bool debug;
		[SerializeField] private bool useSizeAtStart;
		[SerializeField, Conditional(nameof(useSizeAtStart), true, false, false)] private Vector3 size;
		[SerializeField] private float radius;

		protected IHeadProvider headProvider;
		protected Vector3 startingSize;
		protected Renderer[] renderersAtStart;

		public void InjectDependencies([Optional] IHeadProvider headProvider)
		{
			this.headProvider = headProvider;
		}

		protected virtual void Start()
		{
			GetRenderers();
			startingSize = Bounds.size;
		}

		private void GetRenderers()
		{
			if (this == null)
			{
				SpaxDebug.Error("Fetching renderers from non-existing targetable.", $"Entity: {Entity.Identification}");
			}

			if (renderersAtStart == null || renderersAtStart.Any(r => r == null))
			{
				renderersAtStart = GetComponentsInChildren<Renderer>(); ;
			}
		}

		protected void OnDrawGizmos()
		{
			if (!debug)
			{
				return;
			}

			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(Center, Radius);
			Gizmos.color = Color.red;
			Gizmos.DrawSphere(Center, 0.05f);
			Gizmos.color = Color.blue;
			Gizmos.DrawWireCube(Bounds.center, Bounds.size);
		}
	}
}