using UnityEngine;
using SpaxUtils;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="ITargetable"/> <see cref="IEntityComponent"/> implementation.
	/// Provides basic data required for targetting an entity.
	/// </summary>
	public class TargetableComponent : EntityComponentBase, ITargetable
	{
		private const string TOOLTIP_LAZY_CENTER = "Caches the entity's Size on Start() and uses it to calculate the center.\n" +
			"Useful when being used as follow target while animating to prevent jittery movement.";

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
				if (renderersAtStart == null || renderersAtStart.Length < 1)
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
		public Vector3 Center => Application.isPlaying && rigidbodyWrapper != null ? rigidbodyWrapper.CenterOfMass : Bounds.center;

		/// <inheritdoc/>
		public Vector3 Size => Application.isPlaying && useSizeAtStart ? startingSize : Bounds.size;

		/// <inheritdoc/>
		public Vector3 Point => Center;

		/// <inheritdoc/>
		public bool IsTargetable { get; private set; } = true;

		[SerializeField] private bool debug;
		[SerializeField] private bool useSizeAtStart;

		protected RigidbodyWrapper rigidbodyWrapper;
		protected Vector3 startingSize;
		protected Renderer[] renderersAtStart;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		protected virtual void Start()
		{
			GetRenderers();
			startingSize = Bounds.size;
		}

		private void GetRenderers()
		{
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
			Gizmos.DrawSphere(Center, 0.1f);
			Gizmos.color = Color.red;
			Gizmos.DrawSphere(Center, 0.05f);
			Gizmos.color = Color.blue;
			Gizmos.DrawWireCube(Bounds.center, Bounds.size);
		}
	}
}