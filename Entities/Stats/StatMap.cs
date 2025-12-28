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
		public IReadOnlyList<StatMapping> StatMappings
		{
			get
			{
				if (_statMappings == null)
				{
					_statMappings = statMappings.ToList();
					foreach (StatOctadMapping octadMapping in octadMappings)
					{
						StatMapping[] mappings = octadMapping.GetMappings();
						foreach (StatMapping mapping in mappings)
						{
							_statMappings.Add(mapping);
						}
					}
				}
				return _statMappings;
			}
		}
		private List<StatMapping> _statMappings;

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

		/// <summary>
		/// Returns ALL mappings that originate from <paramref name="fromStat"/>.
		/// Useful for equipment or modifiers where one source stat might affect multiple destination stats.
		/// </summary>
		public IEnumerable<StatMapping> GetMappingsFrom(string fromStat)
		{
			foreach (StatMapping m in StatMappings)
			{
				if (m.FromStat == fromStat)
				{
					yield return m;
				}
			}
		}

		/// <summary>
		/// Tries to find a specific mapping connecting <paramref name="fromStat"/> to <paramref name="toStat"/>.
		/// </summary>
		public bool TryGetMapping(string fromStat, string toStat, out StatMapping mapping)
		{
			// We iterate over the full property to ensure we catch Octad-generated mappings too.
			foreach (StatMapping m in StatMappings)
			{
				if (m.FromStat == fromStat && m.ToStat == toStat)
				{
					mapping = m;
					return true;
				}
			}
			mapping = null;
			return false;
		}
	}
}
