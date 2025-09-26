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
	}
}
