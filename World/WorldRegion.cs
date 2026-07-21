using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class WorldRegion : MonoBehaviour, IWorldRegion
	{
		/// <summary>How far past a boundary point to probe when testing whether an abutting sub-region continues there.</summary>
		private const float SEAM_PROBE_OFFSET = 0.05f;

		/// <summary>Above this much X/Z tilt (dot of local up vs world up) the flattened border maths stops being exact.</summary>
		public const float MAX_BORDER_TILT_DOT = 0.999f;

		public enum RegionType
		{
			Box = 0,
			Sphere = 1
		}

		/// <summary>
		/// Activity level of a region, mirrored onto agents bound to it via their spawnpoint.
		/// Active (default 0, backward compatible) = agents behave normally; Sleep = present but dormant; Inactive = disabled.
		/// </summary>
		public enum RegionActivity
		{
			Active = 0,
			Sleep = 1,
			Inactive = 2
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

		/// <summary>Current activity level; agents bound to this region mirror it via <see cref="ActivityChangedEvent"/>.</summary>
		public RegionActivity Activity => activity;

		/// <summary>Invoked when <see cref="Activity"/> changes.</summary>
		public event Action<RegionActivity> ActivityChangedEvent;

		/// <summary>Read-only access to the region list, used by the editor.</summary>
		public IReadOnlyList<Region> Regions => regions;

		/// <summary>All POIs belonging to this region.</summary>
		public IReadOnlyList<PointOfInterest> POIs => pois;

		[SerializeField] private int prio;
		[SerializeField] private RegionActivity activity = RegionActivity.Active;
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

		/// <summary>Sets the region's activity level, notifying bound agents. No-op if unchanged.</summary>
		public void SetActivity(RegionActivity value)
		{
			if (activity == value)
			{
				return;
			}
			activity = value;
			ActivityChangedEvent?.Invoke(value);
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

		/// <inheritdoc/>
		public Vector3 GetClosestPointWithinRegion(Vector3 point)
		{
			if (regions == null || regions.Count == 0)
				return transform.position;

			Vector3 closest = transform.position;
			float closestSqDist = float.MaxValue;

			for (int i = 0; i < regions.Count; i++)
			{
				Vector3 candidate = ClosestPointInRegion(point, i);
				float sqDist = (candidate - point).sqrMagnitude;
				if (sqDist < closestSqDist)
				{
					closestSqDist = sqDist;
					closest = candidate;
				}
			}

			return closest;
		}

		/// <inheritdoc/>
		public bool TryGetBorderDepth(Vector3 point, out float depth, out Vector3 inwardNormal, out float maxDepth)
		{
			depth = 0f;
			inwardNormal = Vector3.zero;
			maxDepth = 0f;

			if (regions == null || regions.Count == 0)
			{
				return false;
			}

			if (cachedWorldCenters == null || cachedWorldCenters.Length != regions.Count)
			{
				BuildCache();
			}

			// Deepest containing sub-region wins: for a union, the sub-region burying the point furthest from its own
			// border is the one that describes the real distance to the outside. maxDepth comes from that same
			// sub-region so the pair always agrees (it can step slightly as an agent crosses a seam).
			bool found = false;
			for (int i = 0; i < regions.Count; i++)
			{
				if (!TryGetRegionBorderDepth(point, i, out float d, out Vector3 n, out float max))
				{
					continue;
				}

				if (!found || d > depth)
				{
					depth = d;
					inwardNormal = n;
					maxDepth = max;
					found = true;
				}
			}

			return found;
		}

		/// <summary>
		/// Border depth within a single sub-region, horizontal only. Candidates whose boundary point lies inside
		/// another sub-region are seams (not real borders) and are skipped.
		/// </summary>
		private bool TryGetRegionBorderDepth(Vector3 point, int index, out float depth, out Vector3 inwardNormal, out float maxDepth)
		{
			depth = 0f;
			inwardNormal = Vector3.zero;
			maxDepth = 0f;

			Region region = regions[index];
			Vector3 worldCenter = cachedWorldCenters[index];

			switch (region.Type)
			{
				case RegionType.Box:
				{
					Quaternion worldRotation = cachedWorldRotations[index];
					Vector3 local = Quaternion.Inverse(worldRotation) * (point - worldCenter);
					float halfX = region.BoxSize.x * 0.5f;
					float halfZ = region.BoxSize.z * 0.5f;
					maxDepth = Mathf.Min(halfX, halfZ);

					// Vertical walls: the footprint doesn't vary with height, so local.y is ignored entirely.
					if (Mathf.Abs(local.x) > halfX || Mathf.Abs(local.z) > halfZ)
					{
						return false;
					}

					// All four horizontal faces are candidates; the nearest non-seam one wins. Evaluating every face
					// (rather than dropping the whole sub-region on a seam) keeps a box's real borders readable while
					// standing at a seam on its opposite side.
					bool found = false;
					for (int axis = 0; axis < 2; axis++)
					{
						float half = axis == 0 ? halfX : halfZ;
						float coord = axis == 0 ? local.x : local.z;

						for (int dir = -1; dir <= 1; dir += 2)
						{
							float faceDepth = half - coord * dir;
							if (found && faceDepth >= depth)
							{
								continue;
							}

							Vector3 localOutward = axis == 0 ? new Vector3(dir, 0f, 0f) : new Vector3(0f, 0f, dir);
							Vector3 localBoundary = local + localOutward * faceDepth;
							Vector3 worldBoundary = worldCenter + worldRotation * localBoundary;
							Vector3 worldOutward = worldRotation * localOutward;

							if (IsSeam(worldBoundary, worldOutward, index))
							{
								continue;
							}

							depth = faceDepth;
							inwardNormal = -worldOutward;
							found = true;
						}
					}

					if (found)
					{
						inwardNormal = inwardNormal.FlattenY().normalized;
					}
					return found;
				}

				case RegionType.Sphere:
				{
					// Slice the sphere at the point's height: the border sits on a circle of radius sqrt(R² - dy²),
					// so height genuinely changes how far the edge is.
					float dy = point.y - worldCenter.y;
					float sqrSlice = region.Radius * region.Radius - dy * dy;
					if (sqrSlice <= 0f)
					{
						return false;
					}

					float sliceRadius = Mathf.Sqrt(sqrSlice);
					maxDepth = sliceRadius;
					Vector3 horizontal = (point - worldCenter).FlattenY();
					float horizontalDist = horizontal.magnitude;
					if (horizontalDist > sliceRadius)
					{
						return false;
					}

					// Dead centre of the slice: maximal depth, and no meaningful direction to push toward.
					if (horizontalDist < 0.0001f)
					{
						depth = sliceRadius;
						inwardNormal = Vector3.zero;
						return true;
					}

					Vector3 outward = horizontal / horizontalDist;
					Vector3 boundary = worldCenter + outward * sliceRadius + Vector3.up * dy;

					// A circle has a single nearest boundary point; if it is a seam, this sub-region simply reports no
					// border (finding the next-nearest unseamed arc isn't worth the intersection maths).
					if (IsSeam(boundary, outward, index))
					{
						return false;
					}

					depth = sliceRadius - horizontalDist;
					inwardNormal = -outward;
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Whether a boundary point is an internal seam rather than a real border: true when another sub-region
		/// continues past it. Probes slightly OUTSIDE the boundary, since an abutting sub-region starts exactly at
		/// the shared face and the point itself is ambiguous.
		/// </summary>
		private bool IsSeam(Vector3 boundaryPoint, Vector3 outward, int ownIndex)
		{
			Vector3 probe = boundaryPoint + outward * SEAM_PROBE_OFFSET;
			for (int i = 0; i < regions.Count; i++)
			{
				if (i != ownIndex && CheckRegion(probe, i))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Whether a box sub-region carries X/Z tilt. <see cref="TryGetBorderDepth"/> works in region-local space and
		/// ignores local Y, which only matches world-horizontal while the region is upright (yaw-only) — a tilted box
		/// yields a border normal with a vertical component. Spheres are rotation-invariant, so never tilted.
		/// </summary>
		public bool IsBorderTilted(int index)
		{
			if (regions == null || index < 0 || index >= regions.Count || regions[index].Type != RegionType.Box)
			{
				return false;
			}

			if (cachedWorldRotations == null || cachedWorldRotations.Length != regions.Count)
			{
				BuildCache();
			}

			return Vector3.Dot(cachedWorldRotations[index] * Vector3.up, Vector3.up) < MAX_BORDER_TILT_DOT;
		}

		private Vector3 ClosestPointInRegion(Vector3 point, int i)
		{
			Region r = regions[i];
			Vector3 wCenter = cachedWorldCenters[i];

			switch (r.Type)
			{
				case RegionType.Box:
					Vector3 local = Quaternion.Inverse(cachedWorldRotations[i]) * (point - wCenter);
					local.x = Mathf.Clamp(local.x, -r.BoxSize.x * 0.5f, r.BoxSize.x * 0.5f);
					local.y = Mathf.Clamp(local.y, -r.BoxSize.y * 0.5f, r.BoxSize.y * 0.5f);
					local.z = Mathf.Clamp(local.z, -r.BoxSize.z * 0.5f, r.BoxSize.z * 0.5f);
					return wCenter + cachedWorldRotations[i] * local;

				case RegionType.Sphere:
					Vector3 toPoint = point - wCenter;
					float mag = toPoint.magnitude;
					return mag <= r.Radius ? point : wCenter + toPoint * (r.Radius / mag);

				default:
					return wCenter;
			}
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
