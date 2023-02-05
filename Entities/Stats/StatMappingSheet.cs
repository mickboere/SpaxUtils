using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// A sheet that allows one stat to map to another.
	/// </summary>
	[CreateAssetMenu(fileName = "StatMappingSheet", menuName = "ScriptableObjects/Stats/Mapping Sheet")]
	public class StatMappingSheet : ScriptableObject
	{
		public IReadOnlyList<StatMapping> Mappings => mappings;

		[SerializeField] private List<StatMapping> mappings;
	}
}