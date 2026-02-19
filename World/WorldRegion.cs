using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class WorldRegion : MonoBehaviour, IWorldRegion
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

		public int Prio => prio;
		public bool HasEntry => entries.Count > 0;
		public bool HasExit => exits.Count > 0;

		[SerializeField] private int prio;
		[SerializeField] private List<Region> regions;
		[SerializeField] private List<Region> entries;
		[SerializeField] private List<Region> exits;
		[SerializeField] private Color gizmosColor;
		[SerializeField] private bool alwaysDrawGizmos;

		protected void OnEnable()
		{
			GlobalDependencyManager.Instance.Get<WorldRegionService>().Register(this);
		}

		protected void OnDisable()
		{
			if (GlobalDependencyManager.HasInstance) // Don't call if application is quiting.
			{
				GlobalDependencyManager.Instance.Get<WorldRegionService>().Remove(this);
			}
		}

		public bool IsInside(Vector3 point)
		{
			return Check(point, regions);
		}

		public bool HasEntered(Vector3 point)
		{
			return Check(point, entries);
		}

		public bool HasExited(Vector3 point)
		{
			return !Check(point, exits);
		}

		private bool Check(Vector3 point, List<Region> regions)
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

		#region Gizmos

		protected void OnDrawGizmos()
		{
			if (alwaysDrawGizmos)
			{
				DrawGizmos();
			}
		}

		protected void OnDrawGizmosSelected()
		{
			if (!alwaysDrawGizmos)
			{
				DrawGizmos();
			}
		}

		private void DrawGizmos()
		{
			if (regions == null)
			{
				return;
			}

			Color fill = new Color(gizmosColor.r, gizmosColor.g, gizmosColor.b, 0.1f);
			Color wire = new Color(gizmosColor.r, gizmosColor.g, gizmosColor.b, 0.9f);
			Draw(regions, fill, wire);
			Draw(entries, fill, Color.green.SetA(0.9f));
			Draw(exits, fill, Color.red.SetA(0.9f));

			void Draw(List<Region> regions, Color fill, Color wire)
			{
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

			#endregion Gizmos
		}
	}
}
