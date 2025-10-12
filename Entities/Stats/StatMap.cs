using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// A sheet that allows one stat to map to another.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(StatMap), menuName = "ScriptableObjects/Stats/" + nameof(StatMap))]
	public class StatMap : ScriptableObject
	{
		public IReadOnlyList<StatMapping> StatMappings => statMappings;

		public IReadOnlyDictionary<string, StatMapping> FromStatMappings
		{
			get
			{
				if (_fromStatMappings == null)
				{
					_fromStatMappings = statMappings.ToDictionary((s) => s.FromStat, (s) => s);
				}
				return _fromStatMappings;
			}
		}
		private Dictionary<string, StatMapping> _fromStatMappings;

		/// <summary>
		/// Direct value-to-value data mappings.
		/// </summary>
		public IList<string> DataMappings => dataMappings;

		[SerializeField, TextArea] private string notes;
		[SerializeField, FormerlySerializedAs("mappings")] private List<StatMapping> statMappings;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers)),
			Tooltip("Direct value-to-value data mappings.")]
		private List<string> dataMappings;
	}
}