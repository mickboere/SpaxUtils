using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "SurfaceLibrary", menuName = "ScriptableObjects/SurfaceLibrary")]
	public class SurfaceLibrary : ScriptableObject
	{
		[SerializeField] private List<SurfaceConfiguration> surfaces;

		public SurfaceConfiguration Get(string surface)
		{
			return surfaces.FirstOrDefault((s) => s.Surface == surface);
		}
	}
}
