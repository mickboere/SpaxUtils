using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "SurfaceLibrary", menuName = "ScriptableObjects/SurfaceLibrary")]
	public class SurfaceLibrary : ScriptableObject, IService
	{
		[SerializeField] private List<SurfaceConfiguration> surfaces;

		private Dictionary<string, SurfaceConfiguration> surfaceCache;

		public SurfaceConfiguration Get(string surface)
		{
			if (surfaceCache == null)
			{
				surfaceCache = new Dictionary<string, SurfaceConfiguration>();
				foreach (SurfaceConfiguration config in surfaces)
				{
					surfaceCache.Add(config.Surface, config);
				}
			}

			if (surfaceCache.ContainsKey(surface))
			{
				return surfaceCache[surface];
			}
			return null;
		}

		public bool TryGet(string surface, out SurfaceConfiguration surfaceConfiguration)
		{
			surfaceConfiguration = Get(surface);
			return surfaceConfiguration != null;
		}

		public void BuildSurfaceData(RaycastHit hit, Dictionary<SurfaceConfiguration, float> result)
		{
			SurfaceComponent.TryGetSurfaceValues(hit, out Dictionary<string, float> surfaces);
			if (surfaces != null && surfaces.Count > 0)
			{
				foreach (KeyValuePair<string, float> surface in surfaces)
				{
					if (TryGet(surface.Key, out SurfaceConfiguration config))
					{
						result[config] = surface.Value;
					}
				}
			}
			else
			{
				result[Get(DefaultSurfaceTypes.DEFAULT)] = 1f;
			}
		}

		public Dictionary<SurfaceConfiguration, float> BuildSurfaceData(RaycastHit hit)
		{
			Dictionary<SurfaceConfiguration, float> result = new Dictionary<SurfaceConfiguration, float>();
			BuildSurfaceData(hit, result);
			return result;
		}
	}
}
