using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;

namespace SpaxUtils
{
	/// <summary>
	/// Trail renderer for weapon swings.
	/// Edited from https://github.com/jojo59516/StickWeaponTrailEffect
	/// </summary>
	public class WeaponTrail : MonoBehaviour
	{
		public struct Shot
		{
			public readonly float timeStamp;
			public readonly Vector3 top;
			public readonly Vector3 bottom;

			public Vector3 center => (top + bottom) * 0.5f;
			public Vector3 radius => (top - bottom) * 0.5f;

			public Shot(float timeStamp, Vector3 top, Vector3 bottom)
			{
				this.timeStamp = timeStamp;
				this.top = top;
				this.bottom = bottom;
			}
		}

		private const int Y_COUNT = 3;

		#region Properties

		/// <summary>
		/// How long each trail segment should last for.
		/// </summary>
		public float Duration
		{
			get => duration;
			set => duration = Math.Max(0f, value);
		}

		/// <summary>
		/// The degree interval between segment divisions.
		/// </summary>
		public float DegreeResolution
		{
			get => degreeResolution;
			set => degreeResolution = Math.Max(1f, value);
		}

		public Transform Bottom
		{
			get => bottom;
			set => bottom = value;
		}

		public Transform Top
		{
			get => top;
			set => top = value;
		}

		public Material Material
		{
			get => material;
			set => material = value;
		}

		public Mesh Mesh
		{
			get
			{
				if (_mesh == null)
				{
					_mesh = new Mesh { name = "Trail Effect" };
					_mesh.MarkDynamic();
				}
				return _mesh;
			}
		}
		private Mesh _mesh = null;

		/// <summary>
		/// Whether the trail should currently be casting.
		/// If false, no new trail segments will be created.
		/// </summary>
		public bool Cast { get; set; } = true;

		public GrowingRingBuffer<Shot> ShotBuffer => _shotBuffer ?? (_shotBuffer = new GrowingRingBuffer<Shot>(0));
		private GrowingRingBuffer<Shot> _shotBuffer;

		#endregion Properties

		[SerializeField] private float duration;
		[SerializeField, Range(1f, 30f)] private float degreeResolution = 15f;
		[SerializeField] private Transform bottom;
		[SerializeField] private Transform top;
		[SerializeField] private Material material;
		[SerializeField] private bool debug;

		protected void LateUpdate()
		{
			Profiler.BeginSample(nameof(LateUpdate));

			UpdateFrameBuffer();

			if (ShotBuffer.Count > 1)
			{
				int verticesMaxCount = ShotBuffer.Count * Mathf.CeilToInt(180f / DegreeResolution);
				NativeArray<Vector3> vertices = new NativeArray<Vector3>(verticesMaxCount * Y_COUNT, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
				int verticesCount = UpdateSegments(vertices);
				UpdateMesh(vertices, verticesCount);
				Graphics.DrawMesh(Mesh, Matrix4x4.identity, Material, gameObject.layer, null, 0, null, false, true, true);
				vertices.Dispose();
			}
			else
			{
				Mesh.Clear(true);
			}

			Profiler.EndSample();
		}

		private void OnDestroy()
		{
			if (_mesh != null)
			{
				Destroy(_mesh);
			}
		}

		private void OnDisable()
		{
			_shotBuffer?.Clear();
		}

		private void UpdateFrameBuffer()
		{
			Profiler.BeginSample(nameof(UpdateFrameBuffer));
			var time = Time.time;

			while (!ShotBuffer.IsEmpty)
			{
				if (time < ShotBuffer[0].timeStamp + Duration)
					break;

				ShotBuffer.Pop();
			}

			if (Cast)
			{
				ShotBuffer.Add(new Shot(time, Top.position, Bottom.position));
			}
			Profiler.EndSample();
		}

		private int UpdateSegments(NativeArray<Vector3> vertices)
		{
			Profiler.BeginSample(nameof(UpdateSegments));
			int count = ShotBuffer.Count;
			int verticesCount = 0;
			for (int i = 0, j = 1; j < count; ++i, ++j)
			{
				Shot stickshotP0 = ShotBuffer[i];
				Shot stickshotP1 = ShotBuffer[j];
				Shot stickshotC0 = i > 0 ? ShotBuffer[i - 1] : stickshotP0;
				Shot stickshotC1 = j + 1 < count ? ShotBuffer[j + 1] : stickshotP1;
				Vector3 centerP0 = stickshotP0.center, radiusP0 = stickshotP0.radius;
				Vector3 centerP1 = stickshotP1.center, radiusP1 = stickshotP1.radius;
				Vector3 centerC0 = stickshotC0.center, radiusC0 = stickshotC0.radius;
				Vector3 centerC1 = stickshotC1.center, radiusC1 = stickshotC1.radius;
				float deltaDegrees = Math.Max(
					Vector3.Angle(centerP1 - centerC0, centerC1 - centerP0),
					Vector3.Angle(radiusP0, radiusP1)
				);
				int interpolations = Mathf.CeilToInt(deltaDegrees / DegreeResolution) + 1;
				if (interpolations > 1)
				{
					for (int k = 0; k < interpolations; ++k)
					{
						float t = (float)k / interpolations;
						Vector3 center = CatmullRom.Sample(centerC0, centerP0, centerP1, centerC1, t);
						Vector3 radius = CatmullRom.Sample(radiusC0, radiusP0, radiusP1, radiusC1, t);
						vertices[verticesCount++] = center - radius; // bottom
						vertices[verticesCount++] = center; // center
						vertices[verticesCount++] = center + radius; // top
					}
				}
				else
				{
					Vector3 center = stickshotP0.center;
					Vector3 radius = stickshotP0.radius;
					vertices[verticesCount++] = center - radius; // bottom
					vertices[verticesCount++] = center; // center
					vertices[verticesCount++] = center + radius; // top
				}
			}

			{
				Shot stickshot = ShotBuffer[count - 1];
				Vector3 center = stickshot.center;
				Vector3 radius = stickshot.radius;
				vertices[verticesCount++] = center - radius; // bottom
				vertices[verticesCount++] = center; // center
				vertices[verticesCount++] = center + radius; // top
			}
			Profiler.EndSample();

			return verticesCount;
		}

		private void UpdateMesh(NativeArray<Vector3> vertices, int verticesCount)
		{
			int shotCount = verticesCount / Y_COUNT;
			int segmentCount = shotCount - 1;
			int indicesCountPerSegment = (Y_COUNT - 1) * 6;
			NativeArray<int> indices = new NativeArray<int>(segmentCount * indicesCountPerSegment, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			NativeArray<Vector2> uv0 = new NativeArray<Vector2>(verticesCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

			Profiler.BeginSample(nameof(UpdateMesh));
			for (int i = 0; i < segmentCount; ++i)
			{
				int leftBottom = i * Y_COUNT;
				int leftCenter = i * Y_COUNT + 1;
				int leftTop = i * Y_COUNT + 2;
				int rightBottom = (i + 1) * Y_COUNT;
				int rightCenter = (i + 1) * Y_COUNT + 1;
				int rightTop = (i + 1) * Y_COUNT + 2;

				int baseIndex = i * indicesCountPerSegment;
				indices[baseIndex++] = leftBottom;
				indices[baseIndex++] = leftCenter;
				indices[baseIndex++] = rightCenter;
				indices[baseIndex++] = rightCenter;
				indices[baseIndex++] = rightBottom;
				indices[baseIndex++] = leftBottom;
				indices[baseIndex++] = leftCenter;
				indices[baseIndex++] = leftTop;
				indices[baseIndex++] = rightTop;
				indices[baseIndex++] = rightTop;
				indices[baseIndex++] = rightCenter;
				indices[baseIndex] = leftCenter;
			}

			for (int i = 0; i < shotCount; ++i)
			{
				float u = 1f - (float)i / segmentCount;
				uv0[i * Y_COUNT] = new Vector2(u, 0f);
				uv0[i * Y_COUNT + 1] = new Vector2(u, 0.5f);
				uv0[i * Y_COUNT + 2] = new Vector2(u, 1f);
			}

			Mesh.Clear(true);
			Mesh.SetVertices(vertices, 0, verticesCount);
			Mesh.SetIndices(indices, 0, segmentCount * indicesCountPerSegment, MeshTopology.Triangles, 0);
			Mesh.SetUVs(0, uv0, 0, verticesCount);
			Profiler.EndSample();

			indices.Dispose();
			uv0.Dispose();
		}

#if UNITY_EDITOR
		protected void OnDrawGizmos()
		{
			if (!debug)
			{
				return;
			}

			Vector3[] vertices = Mesh.vertices;
			int interpolatedCount = vertices.Length / Y_COUNT;
			if (interpolatedCount > ShotBuffer.Count)
			{
				Gizmos.color = Color.green;
				for (int i = 0; i < interpolatedCount; ++i)
				{
					int baseIndex = i * Y_COUNT;
					Gizmos.DrawLine(vertices[baseIndex], vertices[baseIndex + 1]);
					Gizmos.DrawLine(vertices[baseIndex + 1], vertices[baseIndex + 2]);
				}
			}
			Gizmos.color = Color.red;
			for (int i = 0; i < ShotBuffer.Count; ++i)
			{
				Shot stickshot = ShotBuffer[i];
				Gizmos.DrawLine(stickshot.bottom, stickshot.top);
			}
		}
#endif
	}
}
