using System.Collections.Generic;
using UnityEngine;
using System;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IVisionComponent"/> implementation with configurable eyes.
	/// </summary>
	public class VisionComponent : EntityComponentMono, IVisionComponent
	{
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

		/// <summary>
		/// Returns the viewpoint transform to use for vision checks.
		/// Uses the agent's eye transform by default.
		/// </summary>
		/// <param name="useCameraIfAvailable">If true and a camera is present, returns the camera transform instead.</param>
		public Transform GetViewPoint(bool useCameraIfAvailable = false)
		{
			if (useCameraIfAvailable && camera != null)
			{
				return camera.transform;
			}

			if (eyeTransform == null && !string.IsNullOrEmpty(transformIdentifier))
			{
				eyeTransform = transformLookup.Lookup(transformIdentifier);
			}

			return eyeTransform;
		}

		/// <summary>
		/// Returns the field of view to use for vision checks.
		/// Uses the agent's configured FOV by default.
		/// </summary>
		/// <param name="useCameraIfAvailable">If true and a camera is present, returns the camera's field of view instead.</param>
		public float GetFOV(bool useCameraIfAvailable = false)
		{
			if (useCameraIfAvailable && camera != null)
			{
				return camera.fieldOfView;
			}

			return fov;
		}

		/// <inheritdoc/>
		public List<ITargetable> Spot(IEnumerable<ITargetable> targetables, bool useCameraIfAvailable = false)
		{
			List<ITargetable> spotted = new List<ITargetable>();

			Transform vp = GetViewPoint(useCameraIfAvailable);
			if (vp == null)
			{
				SpaxDebug.Error("Eye transform could not be found.", $"Eye transform identifier: {transformIdentifier}", this);
				return spotted;
			}

			Vector3 eyePos = vp.position;
			Vector3 forward = vp.forward;
			float halfFovRad = GetFOV(useCameraIfAvailable) * 0.5f * Mathf.Deg2Rad;
			float cosHalfFov = Mathf.Cos(halfFovRad);
			float rangeSqr = range * range;

			foreach (ITargetable targetable in targetables)
			{
				if (targetable == null)
				{
					continue;
				}

				if (targetable is MonoBehaviour mb && !mb)
				{
					continue;
				}

				Vector3 toTarget = targetable.Center - eyePos;
				float distSqr = toTarget.sqrMagnitude;

				if (distSqr >= rangeSqr)
				{
					continue;
				}

				float dist = Mathf.Sqrt(distSqr);
				Vector3 dir = toTarget / dist;
				float dot = Vector3.Dot(forward, dir);

				if (dot <= cosHalfFov)
				{
					continue;
				}

				if (!Physics.Raycast(eyePos, dir, dist, layerMask))
				{
					spotted.Add(targetable);
				}
			}

			return spotted;
		}

		/// <inheritdoc/>
		public ITargetable GetMostLikelyTarget(IEnumerable<ITargetable> targetables, bool useCameraIfAvailable = false)
		{
			List<ITargetable> visible = Spot(targetables, useCameraIfAvailable);

			Transform vp = GetViewPoint(useCameraIfAvailable);
			if (vp == null)
			{
				return null;
			}

			ITargetable best = null;
			float bestScore = 0f;

			foreach (ITargetable targetable in visible)
			{
				if (targetable == null)
				{
					continue;
				}

				if (targetable is MonoBehaviour mb && !mb)
				{
					continue;
				}

				float angleScore = Vector3.Normalize(targetable.Center - vp.position).ClampedDot(vp.forward);
				float distanceScore = (Vector3.Distance(targetable.Position, agent.Transform.position) / Range).InvertClamped();
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
			if (!debug)
			{
				return;
			}

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

			if (GetViewPoint() == null)
			{
				return;
			}

			Gizmos.color = Color.white;
			Gizmos.matrix = GetViewPoint().localToWorldMatrix;
			Gizmos.DrawFrustum(Vector3.zero, fov, range * 2f / GetViewPoint().lossyScale.magnitude, 0f, 1f);
		}
	}
}
