using System;
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
		/// <summary>
		/// Only contains generic stat mappings from <see cref="statMappings"/>,
		/// does not include mappings from <see cref="octadMappings"/>.
		/// </summary>
		public IReadOnlyList<StatMapping> StatMappings => statMappings;

		/// <summary>
		/// A dictionary of all stat mappings keyed by their source stat identifier.
		/// Contains both generic mappings from <see cref="statMappings"/> and mappings generated from <see cref="octadMappings"/>.
		/// </summary>
		public IReadOnlyDictionary<string, StatMapping> FromStatMappings
		{
			get
			{
				if (_fromStatMappings == null)
				{
					_fromStatMappings = statMappings.ToDictionary((s) => s.FromStat, (s) => s);
					foreach (StatOctadMapping octadMapping in octadMappings)
					{
						StatMapping[] mappings = octadMapping.GetMappings();
						foreach (StatMapping mapping in mappings)
						{
							if (_fromStatMappings.ContainsKey(mapping.FromStat))
							{
								Debug.LogError($"StatMap '{name}' already contains a mapping from stat '{mapping.FromStat}'.", this);
								continue;
							}
							_fromStatMappings[mapping.FromStat] = mapping;
						}
					}
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
		[SerializeField] private List<StatMapping> statMappings;
		[SerializeField] private List<StatOctadMapping> octadMappings;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers)),
			Tooltip("Direct value-to-value data mappings.")]
		private List<string> dataMappings;
	}
}
