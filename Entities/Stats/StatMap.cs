using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// A sheet that allows one stat to map to another.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(StatMap), menuName = "ScriptableObjects/Stats/" + nameof(StatMap))]
	public class StatMap : ScriptableObject
	{
		public IReadOnlyList<StatMapping> Mappings => mappings;

		public IReadOnlyDictionary<string, StatMapping> FromMappings
		{
			get
			{
				if (_fromMappings == null)
				{
					_fromMappings = mappings.ToDictionary((s) => s.FromStat, (s) => s);
				}
				return _fromMappings;
			}
		}
		private Dictionary<string, StatMapping> _fromMappings;

		[SerializeField, TextArea] private string notes;
		[SerializeField] private List<StatMapping> mappings;
	}
}