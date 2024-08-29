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
		public float Range => range;

		[SerializeField, ConstDropdown(typeof(ITransformLookupIdentifiers), includeEmpty: true)] private string transformIdentifier;
		[SerializeField] private Transform eyeTransform;
		[SerializeField] private float fov;
		[SerializeField] private float range;
		[SerializeField] private bool raycast;
		[SerializeField] private LayerMask layerMask;
		[SerializeField] private bool debug;

		private TransformLookup transformLookup;

		public void InjectDependencies(TransformLookup transformLookup)
		{
			this.transformLookup = transformLookup;
		}

		/// <inheritdoc/>
		public List<ITargetable> Spot(IEnumerable<ITargetable> targetables)
		{
			List<ITargetable> spotted = new List<ITargetable>();

			if (eyeTransform == null && !string.IsNullOrEmpty(transformIdentifier))
			{
				eyeTransform = transformLookup.Lookup(transformIdentifier);
			}

			if (eyeTransform == null)
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

				Vector3 eyeToTarget = targetable.Center - eyeTransform.position;
				float distanceToTarget = eyeToTarget.magnitude;
				if (distanceToTarget < range && Vector3.Angle(eyeTransform.forward, eyeToTarget.normalized) < fov * 0.5f)
				{
					if (!raycast || !Physics.Raycast(eyeTransform.position, eyeToTarget, out _, eyeToTarget.magnitude, layerMask))
					{
						spotted.Add(targetable);
					}
				}
			}

			return spotted;
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

				if (eyeTransform == null)
				{
					//SpaxDebug.Error("Eye transform could not be found.", $"Index: {eyes.IndexOf(eye)}", this);
					return;
				}

				Gizmos.color = Color.white;
				Gizmos.matrix = eyeTransform.localToWorldMatrix;
				Gizmos.DrawFrustum(Vector3.zero, fov, range * 2f / eyeTransform.lossyScale.magnitude, 0f, 1f);
			}
		}
	}
}
