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

		/// <summary>Drops the cache so the next read rebuilds it; runs after deserialization.</summary>
		protected void OnEnable()
		{
			surfaceCache = null;
		}

#if UNITY_EDITOR
		/// <summary>Rebuilds on inspector edits, which would otherwise not apply until the next domain reload.</summary>
		protected void OnValidate()
		{
			surfaceCache = null;
		}
#endif

		public SurfaceConfiguration Get(string surface)
		{
			// Count check as well as null: a read before deserialization completes would otherwise
			// latch an empty dictionary, which is not null and so never rebuilds.
			if (surfaceCache == null || surfaceCache.Count == 0)
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
