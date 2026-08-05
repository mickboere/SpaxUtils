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
				// Count check as well as null: an early reader (edit-mode Entity injection fires before deserialization
				// completes) would otherwise latch an empty list, which is not null and so never rebuilds.
				if (_statMappings == null || _statMappings.Count == 0)
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
		/// Drops the cache so the next read rebuilds it. Runs after deserialization, discarding any cache
		/// an early reader built from incomplete data.
		/// </summary>
		protected void OnEnable()
		{
			_statMappings = null;
		}

#if UNITY_EDITOR
		/// <summary>
		/// Rebuilds on inspector edits, which would otherwise not apply until the next domain reload.
		/// </summary>
		protected void OnValidate()
		{
			_statMappings = null;
		}
#endif

		/// <summary>
		/// Returns ALL mappings that originate from <paramref name="fromStat"/>.
		/// Useful for equipment or modifiers where one source stat might affect multiple destination stats.
		/// </summary>
		/// <param name="includeDisabled">Also return mappings that are switched off in the sheet.</param>
		public IEnumerable<StatMapping> GetMappingsFrom(string fromStat, bool includeDisabled = false)
		{
			foreach (StatMapping m in StatMappings)
			{
				if (m.FromStat == fromStat && (includeDisabled || m.Enabled))
				{
					yield return m;
				}
			}
		}

		/// <summary>
		/// Tries to find a specific mapping connecting <paramref name="fromStat"/> to <paramref name="toStat"/>.
		/// </summary>
		/// <param name="includeDisabled">Also match mappings that are switched off in the sheet.</param>
		public bool TryGetMapping(string fromStat, string toStat, out StatMapping mapping, bool includeDisabled = false)
		{
			// We iterate over the full property to ensure we catch Octad-generated mappings too.
			foreach (StatMapping m in StatMappings)
			{
				if (m.FromStat == fromStat && m.ToStat == toStat && (includeDisabled || m.Enabled))
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
