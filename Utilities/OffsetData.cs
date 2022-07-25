using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class OffsetData
	{
		private const float DEFAULT_GIZMOS_RADIUS = 0.01f;
		private static readonly Color DEFAULT_GIZMOS_COLOR = Color.white;

		public Transform Context { get { return context; } set { context = value; } }
		public Vector3 Offset => offset;
		public Vector3 WorldPosition => Context.position + Context.rotation * offset;

		[SerializeField] private Transform context;
		[SerializeField] private Vector3 offset;
		[SerializeField] private float gizmosRadius;
		[SerializeField] private Color gizmosColor;

		public OffsetData(
			Vector3 offset = new Vector3(),
			float? gizmosRadius = null,
			Color? gizmosColor = null)
		{
			this.offset = offset;

			if (gizmosRadius.HasValue)
				this.gizmosRadius = gizmosRadius.Value;
			else
				this.gizmosRadius = DEFAULT_GIZMOS_RADIUS;

			if (gizmosColor.HasValue)
				this.gizmosColor = gizmosColor.Value;
			else
				this.gizmosColor = DEFAULT_GIZMOS_COLOR;
		}

		public Vector3 GetOffsetFrom(Transform relative)
		{
			return relative.InverseTransformPoint(WorldPosition);
		}

		public Vector3 RotateAroundPivot(Quaternion rotation, Transform offsetFrom)
		{
			return rotation * -GetOffsetFrom(offsetFrom) + GetOffsetFrom(offsetFrom);
		}

		public void DrawGizmos()
		{
			if (Context == null)
			{
				return;
			}

			Gizmos.color = gizmosColor;
			Gizmos.DrawSphere(WorldPosition, gizmosRadius);
		}
	}
}