using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class SpawnRegion : MonoBehaviour
	{
		public enum RegionType
		{
			Box = 0,
			Sphere = 1
		}

		[Serializable]
		public class Region
		{
			public RegionType Type => type;
			public Vector3 BoxSize => boxSize;
			public float Radius => radius;
			public Vector3 Offset => offset;

			[SerializeField] RegionType type;
			[SerializeField, Conditional(nameof(type), 0, hide: false)] Vector3 boxSize;
			[SerializeField, Conditional(nameof(type), 1, hide: false)] private float radius;
			[SerializeField] private Vector3 offset;
		}

		[SerializeField] private List<Region> regions;
		[SerializeField] private bool drawGizmos;
		[SerializeField, Conditional(nameof(drawGizmos), hide: true)] private Color gizmosColor;

		public bool IsInside(Vector3 point)
		{
			foreach (Region region in regions)
			{
				switch (region.Type)
				{
					case RegionType.Box:
						if (new Bounds(transform.position + region.Offset, region.BoxSize).Contains(point))
						{
							return true;
						}
						break;
					case RegionType.Sphere:
						if ((transform.position + region.Offset - point).sqrMagnitude < region.Radius * region.Radius)
						{
							return true;
						}
						break;
				}
			}
			return false;
		}

		protected void OnDrawGizmos()
		{
			if (!drawGizmos)
			{
				return;
			}

			Color fill = new Color(gizmosColor.r, gizmosColor.g, gizmosColor.b, 0.1f);
			Color wire = new Color(gizmosColor.r, gizmosColor.g, gizmosColor.b, 0.9f);

			foreach (Region region in regions)
			{
				switch (region.Type)
				{
					case RegionType.Box:
						Gizmos.color = fill;
						Gizmos.DrawCube(transform.position + region.Offset, region.BoxSize);
						Gizmos.color = wire;
						Gizmos.DrawWireCube(transform.position + region.Offset, region.BoxSize);
						break;
					case RegionType.Sphere:
						Gizmos.color = fill;
						Gizmos.DrawSphere(transform.position + region.Offset, region.Radius);
						Gizmos.color = wire;
						Gizmos.DrawWireSphere(transform.position + region.Offset, region.Radius);
						break;
				}
			}
		}
	}
}
