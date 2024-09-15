using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IVisionComponent"/> implementation with configurable eyes.
	/// </summary>
	public class VisionComponent : EntityComponentBase, IVisionComponent
	{
		/// <inheritdoc/>
		public Transform ViewPoint
		{
			get
			{
				if (camera != null)
				{
					return camera.transform;
				}

				if (eyeTransform == null && !string.IsNullOrEmpty(transformIdentifier))
				{
					eyeTransform = transformLookup.Lookup(transformIdentifier);
				}

				return eyeTransform;
			}
		}

		/// <inheritdoc/>
		public float FOV
		{
			get
			{
				if (camera != null)
				{
					return camera.fieldOfView;
				}
				return fov;
			}
		}

		/// <inheritdoc/>
		public float Range => range;

		[SerializeField, ConstDropdown(typeof(ITransformLookupIdentifiers), includeEmpty: true)] private string transformIdentifier;
		[SerializeField] private Transform eyeTransform;
		[SerializeField] private float fov;
		[SerializeField] private float range;
		[SerializeField] private LayerMask layerMask;
		[SerializeField] private bool debug;

		private IAgent agent;
		private TransformLookup transformLookup;
		new private Camera camera;

		public void InjectDependencies(IAgent agent, TransformLookup transformLookup, [Optional] Camera camera)
		{
			this.agent = agent;
			this.transformLookup = transformLookup;
			this.camera = camera;
		}

		/// <inheritdoc/>
		public List<ITargetable> Spot(IEnumerable<ITargetable> targetables)
		{
			List<ITargetable> spotted = new List<ITargetable>();

			if (ViewPoint == null)
			{
				SpaxDebug.Error("Eye transform could not be found.", $"Eye transform identifier: {transformIdentifier}", this);
				return spotted;
			}

			// For each targetable, check if they are in view (meaning in range and within FOV).
			foreach (ITargetable targetable in targetables)
			{
				if (spotted.Contains(targetable))
				{
					// Skip targetables that have already been spotted.
					continue;
				}

				Vector3 eyeToTarget = targetable.Center - ViewPoint.position;
				float distanceToTarget = eyeToTarget.magnitude;
				if (distanceToTarget < range && Vector3.Angle(ViewPoint.forward, eyeToTarget.normalized) < fov * 0.5f)
				{
					if (!Physics.Raycast(ViewPoint.position, eyeToTarget, out _, eyeToTarget.magnitude, layerMask))
					{
						spotted.Add(targetable);
					}
				}
			}

			return spotted;
		}

		/// <inheritdoc/>
		public ITargetable GetMostLikelyTarget(IEnumerable<ITargetable> targetables)
		{
			List<ITargetable> visible = Spot(targetables);

			ITargetable best = null;
			float bestScore = 0f;
			foreach (ITargetable targetable in visible)
			{
				float angleScore = Vector3.Normalize(targetable.Center - ViewPoint.position).ClampedDot(ViewPoint.forward);
				float distanceScore = (Vector3.Distance(targetable.Position, agent.Transform.position) / Range).ClampedInvert();
				float score = angleScore * 5f + distanceScore;
				if (score > bestScore)
				{
					best = targetable;
					bestScore = score;
				}
			}

			return best;
		}

		protected void OnDrawGizmos()
		{
			if (debug)
			{
				if (eyeTransform == null && !string.IsNullOrEmpty(transformIdentifier))
				{
					if (transformLookup == null)
					{
						transformLookup = GetComponent<TransformLookup>();
					}

					if (transformLookup != null)
					{
						eyeTransform = transformLookup.Lookup(transformIdentifier);
					}
				}

				if (ViewPoint == null)
				{
					return;
				}

				Gizmos.color = Color.white;
				Gizmos.matrix = ViewPoint.localToWorldMatrix;
				Gizmos.DrawFrustum(Vector3.zero, fov, range * 2f / ViewPoint.lossyScale.magnitude, 0f, 1f);
			}
		}
	}
}
