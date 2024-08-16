using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IVisionComponent"/> implementation with configurable eyes.
	/// </summary>
	public class VisionComponent : EntityComponentBase, IVisionComponent
	{
		[Serializable]
		public class Eye
		{
			public string TransformIdentifier => transformIdentifier;
			public Transform Transform
			{
				get { return transform; }
				set { transform = value; }
			}
			public float Fov => fov;
			public float Range => range;
			public bool Raycast => raycast;
			public LayerMask LayerMask => layerMask;

			[SerializeField, ConstDropdown(typeof(ITransformLookupIdentifiers), includeEmpty: true)] private string transformIdentifier;
			[SerializeField] private Transform transform;
			[SerializeField] private float fov;
			[SerializeField] private float range;
			[SerializeField] private bool raycast;
			[SerializeField] private LayerMask layerMask;
		}

		public float Range => eyes[0].Range;

		[SerializeField] private List<Eye> eyes;
		[SerializeField] private bool debug;

		private TransformLookup transformLookup;

		public void InjectDependencies(TransformLookup transformLookup)
		{
			this.transformLookup = transformLookup;
		}

		public List<ITargetable> Spot(IEnumerable<ITargetable> targetables)
		{
			List<ITargetable> spotted = new List<ITargetable>();

			foreach (Eye eye in eyes)
			{
				if (eye.Transform == null && !string.IsNullOrEmpty(eye.TransformIdentifier))
				{
					eye.Transform = transformLookup.Lookup(eye.TransformIdentifier);
				}

				if (eye.Transform == null)
				{
					SpaxDebug.Error("Eye transform could not be found.", $"Index: {eyes.IndexOf(eye)}", this);
					continue;
				}

				// For each targetable, check if they are in view (meaning in range and within FOV).
				foreach (ITargetable targetable in targetables)
				{
					if (spotted.Contains(targetable))
					{
						// Skip targetables that have already been spotted.
						continue;
					}

					Vector3 eyeToTarget = targetable.Center - eye.Transform.position;
					float distanceToTarget = eyeToTarget.magnitude;
					if (distanceToTarget < eye.Range && Vector3.Angle(eye.Transform.forward, eyeToTarget.normalized) < eye.Fov * 0.5f)
					{
						if (!eye.Raycast || !Physics.Raycast(eye.Transform.position, eyeToTarget, out _, eyeToTarget.magnitude, eye.LayerMask))
						{
							spotted.Add(targetable);
						}
					}
				}
			}

			return spotted;
		}

		protected void OnDrawGizmos()
		{
			if (debug)
			{
				foreach (Eye eye in eyes)
				{
					if (eye.Transform == null && !string.IsNullOrEmpty(eye.TransformIdentifier))
					{
						if (transformLookup == null)
						{
							transformLookup = GetComponent<TransformLookup>();
						}

						if (transformLookup != null)
						{
							eye.Transform = transformLookup.Lookup(eye.TransformIdentifier);
						}
					}

					if (eye.Transform == null)
					{
						//SpaxDebug.Error("Eye transform could not be found.", $"Index: {eyes.IndexOf(eye)}", this);
						continue;
					}

					Gizmos.color = Color.white;
					Gizmos.matrix = eye.Transform.localToWorldMatrix;
					Gizmos.DrawFrustum(Vector3.zero, eye.Fov, eye.Range * 2f / eye.Transform.lossyScale.magnitude, 0f, 1f);
				}
			}
		}
	}
}
