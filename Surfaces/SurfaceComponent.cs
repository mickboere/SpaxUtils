using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Component for defining the surface types of an object.
	/// Contains support for vertex colors and retrieving the different surface values from those colors.
	/// </summary>
	public class SurfaceComponent : MonoBehaviour
	{
		[Serializable]
		public class SurfaceData
		{
			[ConstDropdown(typeof(ISurfaceTypeConstants))] public string surfaceType;
			public ColorChannel vertexColor;
		}

		[SerializeField, ConstDropdown(typeof(ISurfaceTypeConstants))] private string defaultSurfaceType;
		[SerializeField] private List<SurfaceData> surfaces;

		protected void OnValidate()
		{
			EnsureOneSurfacePerColor();
		}

		#region Static Methods

		/// <summary>
		/// Static method to try and get surface values from any given <paramref name="raycastHit"/>.
		/// </summary>
		public static bool TryGetSurfaceValues(RaycastHit raycastHit, out Dictionary<string, float> surfaces)
		{
			surfaces = null;
			if (raycastHit.transform != null && raycastHit.transform.gameObject.TryGetComponentRelative(out SurfaceComponent component))
			{
				surfaces = component.GetSurfaceValues(raycastHit);
			}
			return surfaces != null;
		}

		/// <summary>
		/// Retrieves the vertex color from <paramref name="mesh"/> at <paramref name="point"/>.
		/// </summary>
		/// <param name="transform">The transform holding the mesh.</param>
		/// <param name="mesh">The mesh to retrieve the vertex color from.</param>
		/// <param name="triangleIndex">The index of the specific triangle the <paramref name="point"/> falls in.</param>
		/// <param name="point">The point on the mesh in world space.</param>
		/// <returns>The vertex color of <paramref name="mesh"/> at <paramref name="point"/>.</returns>
		public static Color GetVertexColorAtPoint(Transform transform, Mesh mesh, int triangleIndex, Vector3 point)
		{
			int[] triangles = mesh.triangles;
			int vertIndexA = triangles[triangleIndex * 3 + 0];
			int vertIndexB = triangles[triangleIndex * 3 + 1];
			int vertIndexC = triangles[triangleIndex * 3 + 2];

			Vector3 vertA = transform.TransformPoint(mesh.vertices[vertIndexA]);
			Vector3 vertB = transform.TransformPoint(mesh.vertices[vertIndexB]);
			Vector3 vertC = transform.TransformPoint(mesh.vertices[vertIndexC]);

			Vector3 barycentric = Vector3Extensions.Barycentric(vertA, vertB, vertC, point);

			Color[] colors = mesh.colors;
			Color vertColorA = colors[vertIndexA] * barycentric.x;
			Color vertColorB = colors[vertIndexB] * barycentric.y;
			Color vertColorC = colors[vertIndexC] * barycentric.z;

			Color pointColor = vertColorA + vertColorB + vertColorC;
			return pointColor;
		}

		/// <summary>
		/// Retrieve the float value of one specific color channel using the <see cref="ColorChannel"/> enum.
		/// </summary>
		/// <param name="color">The color to retrieve the channel from.</param>
		/// <param name="channel">The channel to retrieve from the color.</param>
		/// <returns>The float value of <paramref name="channel"/> in <paramref name="color"/>.</returns>
		public static float GetColorValue(Color color, ColorChannel channel)
		{
			switch (channel)
			{
				case ColorChannel.Red:
					return color.r;
				case ColorChannel.Green:
					return color.g;
				case ColorChannel.Blue:
					return color.b;
				case ColorChannel.Alpha:
					return color.a;
				default:
					SpaxDebug.Error("Color channel does not exist:", channel.ToString());
					return 0f;
			}
		}

		public static Color GetChannelAsColor(ColorChannel channel)
		{
			switch (channel)
			{
				case ColorChannel.Red:
					return new Color(1f, 0f, 0f, 0f);
				case ColorChannel.Green:
					return new Color(0f, 1f, 0f, 0f);
				case ColorChannel.Blue:
					return new Color(0f, 0f, 1f, 0f);
				case ColorChannel.Alpha:
					return new Color(0f, 0f, 0f, 1f);
				default:
					SpaxDebug.Error("Color channel does not exist:", channel.ToString());
					return new Color(0f, 0f, 0f, 0f);
			}
		}

		#endregion

		/// <summary>
		/// Retrieves a dictionary of normalized surface values located at the <paramref name="raycastHit"/>.
		/// </summary>
		/// <param name="raycastHit">The hit to retrieve the surface data from.</param>
		/// <returns>A dictionary of normalized surface values located at the <paramref name="raycastHit"/>.</returns>
		public Dictionary<string, float> GetSurfaceValues(RaycastHit raycastHit)
		{
			if (surfaces.Count == 0)
			{
				return new Dictionary<string, float>() { { defaultSurfaceType, 1f } };
			}

			if (!raycastHit.transform.gameObject.TryGetComponentRelative(out MeshFilter meshFilter))
			{
				Debug.LogError("No MeshFilter found to read vertex colors from, returning null.", raycastHit.transform);
				return null;
			}

			Mesh mesh = meshFilter.sharedMesh;
			if (!mesh.isReadable)
			{
				Debug.LogError("Mesh is not readable, returning null.", mesh);
				return null;
			}

			return GetSurfaceValues(meshFilter.transform, mesh, raycastHit.triangleIndex, raycastHit.point);
		}

		/// <summary>
		/// Retrieves a dictionary of normalized surface values located at <paramref name="point"/>.
		/// </summary>
		/// <param name="transform">The transform holding the mesh.</param>
		/// <param name="mesh">The mesh containing the vertex colors used to calculate the surface values.</param>
		/// <param name="triangleIndex">The specific triangle the <paramref name="point"/> falls in.</param>
		/// <param name="point">The point on the mesh in world space.</param>
		/// <returns></returns>
		public Dictionary<string, float> GetSurfaceValues(Transform transform, Mesh mesh, int triangleIndex, Vector3 point)
		{
			Dictionary<string, float> surfaceValues = new Dictionary<string, float>();
			if (surfaces.Count > 0)
			{
				// Collect vertex color at point and create channel ordered blend of it.
				Color color = GetVertexColorAtPoint(transform, mesh, triangleIndex, point);
				Color blend = new Color(color.r, 0f, 0f, 0f)
					.Lerp(new Color(0f, 1f, 0f, 0f), color.g)
					.Lerp(new Color(0f, 0f, 1f, 0f), color.b)
					.Lerp(new Color(0f, 0f, 0f, 1f), color.a);

				// For each surface, add it's color channel value to the surface values.
				foreach (SurfaceData surface in surfaces)
				{
					surfaceValues[surface.surfaceType] = GetColorValue(blend, surface.vertexColor);
				}

				// Add default surface to fill out default audio.
				if (!surfaceValues.ContainsKey(defaultSurfaceType)) { surfaceValues.Add(defaultSurfaceType, 0f); }
				surfaceValues[defaultSurfaceType] += blend.Sum().Invert();
			}
			else
			{
				surfaceValues.Add(defaultSurfaceType, 1f);
			}

			return surfaceValues;
		}

		private void AddSurfaceValueToDictionary(Dictionary<string, float> surfaceValues, string surface, float value)
		{
			if (!surfaceValues.ContainsKey(surface))
			{
				surfaceValues.Add(surface, 0f);
			}

			surfaceValues[surface] += value;
		}

		private void EnsureOneSurfacePerColor()
		{
			if (surfaces != null && surfaces.Count > 1)
			{
				SurfaceData lastEntry = surfaces[surfaces.Count - 1];
				if (surfaces.Count > 4)
				{
					Debug.LogError("There's only 4 available color channels, adding a fifth surface is prohibited.");
					surfaces.Remove(lastEntry);
				}
				else
				{
					// Find unused vertex color.
					while (surfaces.Any((x) => x != lastEntry && x.vertexColor == lastEntry.vertexColor))
					{
						int color = (int)lastEntry.vertexColor;
						lastEntry.vertexColor = (ColorChannel)(color + 1 > 3 ? 0 : color + 1);
					}
				}
			}
		}
	}
}
