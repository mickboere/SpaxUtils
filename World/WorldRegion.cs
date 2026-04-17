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
			/// <summary>Center offset in WorldRegion local space.</summary>
			public Vector3 Offset => offset;
			/// <summary>Rotation in WorldRegion local space (euler angles). Applies to boxes only; stored for spheres so it is retained on type change.</summary>
			public Vector3 Rotation => rotation;

			[SerializeField] private RegionType type;
			[SerializeField] private Vector3 boxSize = Vector3.one;
			[SerializeField] private float radius = 1f;
			[SerializeField] private Vector3 offset;
			[SerializeField] private Vector3 rotation;
		}

		public int Prio => prio;

		/// <summary>Read-only access to the region list, used by the editor.</summary>
		public IReadOnlyList<Region> Regions => regions;

		/// <summary>All POIs belonging to this region.</summary>
		public IReadOnlyList<PointOfInterest> POIs => pois;

		[SerializeField] private int prio;
		[SerializeField] private List<Region> regions = new List<Region>();
		[SerializeField] private Color gizmosColor = Color.cyan;
		[SerializeField] private bool alwaysDrawGizmos;

		[Header("Points of Interest")]
		[SerializeField] private bool autoCollectPois;
		[SerializeField] private List<PointOfInterest> pois = new List<PointOfInterest>();

		// Cached per-region data, computed once on Awake since regions are static at runtime.
		private Vector3[] cachedWorldCenters;
		private Quaternion[] cachedWorldRotations;
		private float[] cachedVolumes;
		private float[] cachedCumulativeVolumes;
		private float cachedTotalVolume;

		protected void Awake()
		{
			if (autoCollectPois)
			{
				GetComponentsInChildren(true, pois);
			}

			BuildCache();
		}

		public void BuildCache()
		{
			int count = regions != null ? regions.Count : 0;

			cachedWorldCenters = new Vector3[count];
			cachedWorldRotations = new Quaternion[count];
			cachedVolumes = new float[count];
			cachedCumulativeVolumes = new float[count];
			cachedTotalVolume = 0f;

			for (int i = 0; i < count; i++)
			{
				cachedWorldCenters[i] = transform.TransformPoint(regions[i].Offset);
				cachedWorldRotations[i] = transform.rotation * Quaternion.Euler(regions[i].Rotation);
				cachedVolumes[i] = RegionVolume(regions[i]);
				cachedTotalVolume += cachedVolumes[i];
				cachedCumulativeVolumes[i] = cachedTotalVolume;
			}
		}

		protected void OnEnable()
		{
			GlobalDependencyManager.Instance.Get<WorldRegionService>().Register(this);
		}

		protected void OnDisable()
		{
			if (GlobalDependencyManager.HasInstance)
			{
				GlobalDependencyManager.Instance.Get<WorldRegionService>().Remove(this);
			}
		}

		/// <inheritdoc/>
		public bool IsInside(Vector3 point)
		{
			for (int i = 0; i < regions.Count; i++)
			{
				if (CheckRegion(point, i))
				{
					return true;
				}
			}
			return false;
		}

		/// <inheritdoc/>
		public Vector3 SamplePoint()
		{
			if (regions == null || regions.Count == 0)
			{
				return transform.position;
			}

			if (cachedTotalVolume <= 0f)
			{
				return transform.position;
			}

			float roll = UnityEngine.Random.Range(0f, cachedTotalVolume);
			int selectedIndex = regions.Count - 1;

			for (int i = 0; i < cachedCumulativeVolumes.Length; i++)
			{
				if (roll <= cachedCumulativeVolumes[i])
				{
					selectedIndex = i;
					break;
				}
			}

			return SampleRegion(selectedIndex);
		}

		/// <summary>
		/// Returns all unoccupied POIs in this region whose parent entity has all of the given labels.
		/// Passing null or empty <paramref name="requiredLabels"/> matches any POI.
		/// </summary>
		public List<PointOfInterest> GetAvailablePOIs(string[] requiredLabels = null)
		{
			List<PointOfInterest> result = new List<PointOfInterest>();
			bool filterByLabels = requiredLabels != null && requiredLabels.Length > 0;

			foreach (PointOfInterest poi in pois)
			{
				if (!poi.IsOccupied && (!filterByLabels || poi.Entity.Identification.HasAll(requiredLabels)))
				{
					result.Add(poi);
				}
			}

			return result;
		}

		// -------------------------------------------------------------------------
		// Helpers
		// -------------------------------------------------------------------------

		/// <summary>
		/// Returns the cached world-space center of a region by index.
		/// </summary>
		public Vector3 GetWorldCenter(int index)
		{
			if (cachedWorldCenters == null || index >= cachedWorldCenters.Length)
			{
				BuildCache();
			}

			return cachedWorldCenters[index];
		}

		/// <summary>
		/// Returns the cached full world-space rotation of a region by index.
		/// </summary>
		public Quaternion GetWorldRotation(int index)
		{
			if (cachedWorldRotations == null || index >= cachedWorldRotations.Length)
			{
				BuildCache();
			}

			return cachedWorldRotations[index];
		}

		/// <summary>
		/// Returns the cached world-space center of a region.
		/// </summary>
		public Vector3 GetWorldCenter(Region region)
		{
			int index = regions.IndexOf(region);
			return index >= 0 ? cachedWorldCenters[index] : transform.TransformPoint(region.Offset);
		}

		/// <summary>
		/// Returns the cached full world-space rotation of a region.
		/// </summary>
		public Quaternion GetWorldRotation(Region region)
		{
			int index = regions.IndexOf(region);
			return index >= 0 ? cachedWorldRotations[index] : transform.rotation * Quaternion.Euler(region.Rotation);
		}

		private bool CheckRegion(Vector3 point, int index)
		{
			Region region = regions[index];
			Vector3 worldCenter = cachedWorldCenters[index];

			switch (region.Type)
			{
				case RegionType.Box:
					Vector3 localPoint = Quaternion.Inverse(cachedWorldRotations[index]) * (point - worldCenter);
					return Mathf.Abs(localPoint.x) <= region.BoxSize.x * 0.5f &&
						   Mathf.Abs(localPoint.y) <= region.BoxSize.y * 0.5f &&
						   Mathf.Abs(localPoint.z) <= region.BoxSize.z * 0.5f;

				case RegionType.Sphere:
					return (worldCenter - point).sqrMagnitude < region.Radius * region.Radius;
			}

			return false;
		}

		private float RegionVolume(Region region)
		{
			switch (region.Type)
			{
				case RegionType.Box:
					return region.BoxSize.x * region.BoxSize.y * region.BoxSize.z;
				case RegionType.Sphere:
					return (4f / 3f) * Mathf.PI * region.Radius * region.Radius * region.Radius;
				default:
					return 0f;
			}
		}

		private Vector3 SampleRegion(int index)
		{
			Region region = regions[index];
			Vector3 worldCenter = cachedWorldCenters[index];
			Quaternion fullRotation = cachedWorldRotations[index];

			switch (region.Type)
			{
				case RegionType.Box:
					// Sample in region local space, then rotate to world space.
					Vector3 localSample = new Vector3(
						UnityEngine.Random.Range(-region.BoxSize.x * 0.5f, region.BoxSize.x * 0.5f),
						UnityEngine.Random.Range(-region.BoxSize.y * 0.5f, region.BoxSize.y * 0.5f),
						UnityEngine.Random.Range(-region.BoxSize.z * 0.5f, region.BoxSize.z * 0.5f));
					return worldCenter + fullRotation * localSample;

				case RegionType.Sphere:
					// Rejection sampling for unbiased uniform distribution within sphere.
					for (int attempt = 0; attempt < 30; attempt++)
					{
						Vector3 candidate = new Vector3(
							UnityEngine.Random.Range(-1f, 1f),
							UnityEngine.Random.Range(-1f, 1f),
							UnityEngine.Random.Range(-1f, 1f));

						if (candidate.sqrMagnitude <= 1f)
						{
							return worldCenter + candidate * region.Radius;
						}
					}
					// Fallback: statistically unreachable under normal use.
					return worldCenter + UnityEngine.Random.onUnitSphere * region.Radius;

				default:
					return worldCenter;
			}
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
			if (regions == null || regions.Count == 0)
			{
				return;
			}

			if (cachedWorldCenters == null || cachedWorldCenters.Length != regions.Count)
			{
				BuildCache();
			}

			Color fill = new Color(gizmosColor.r, gizmosColor.g, gizmosColor.b, 0.1f);
			Color wire = new Color(gizmosColor.r, gizmosColor.g, gizmosColor.b, 0.9f);

			for (int i = 0; i < regions.Count; i++)
			{
				DrawRegionGizmo(regions[i], cachedWorldCenters[i], cachedWorldRotations[i], fill, wire);
			}
		}

		private void DrawRegionGizmo(Region region, Vector3 worldCenter, Quaternion worldRotation, Color fill, Color wire)
		{
			switch (region.Type)
			{
				case RegionType.Box:
					Matrix4x4 oldMatrix = Gizmos.matrix;
					Gizmos.matrix = Matrix4x4.TRS(worldCenter, worldRotation, Vector3.one);
					Gizmos.color = fill;
					Gizmos.DrawCube(Vector3.zero, region.BoxSize);
					Gizmos.color = wire;
					Gizmos.DrawWireCube(Vector3.zero, region.BoxSize);
					Gizmos.matrix = oldMatrix;
					break;

				case RegionType.Sphere:
					Gizmos.color = fill;
					Gizmos.DrawSphere(worldCenter, region.Radius);
					Gizmos.color = wire;
					Gizmos.DrawWireSphere(worldCenter, region.Radius);
					break;
			}
		}

		#endregion
	}
}
