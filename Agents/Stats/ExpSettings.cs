using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// The single location for all EXP reward balancing.
	/// EXP is rewarded in BARS: 1 bar equals a full emptying of the element's point-stat, so every deed
	/// is comparable across the eight elements regardless of what it was measured in.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(ExpSettings), menuName = "ScriptableObjects/Stats/" + nameof(ExpSettings))]
	public class ExpSettings : ScriptableObject, IService
	{
		#region Tooltips
		private const string TT_multiplier = "Global EXP multiplier, applied last. Turns the entire economy up or down at once.";
		private const string TT_elementWeights = "Per-element base weight in octad order (Fire, Light, Air, Spirit, Water, Nature, Earth, Void). Multiplies every reward for that element.";
		private const string TT_sources = "Per-source weight and anti-farm decay. Sources without an entry run at weight 1 with no decay.";
		#endregion

		/// <summary>
		/// A single reward source's balancing data. A class, not a struct, so new inspector entries start at weight 1.
		/// </summary>
		[Serializable]
		public class Source
		{
			[SerializeField, ConstDropdown(typeof(IExpSources))] public string source;
			[SerializeField, Tooltip("Multiplies this source's reward, on top of the element weight.")] public float weight = 1f;
			[SerializeField, Min(0f), Tooltip("Seconds for a full bar's worth of decay to recover. This IS the source's sustained ceiling: it can never pay more than 1 bar per decayTime. 0 disables decay.")] public float decayTime;
		}

		public float Multiplier => multiplier;
		public Vector8 ElementWeights => elementWeights;

		[SerializeField, Min(0f), Tooltip(TT_multiplier)] private float multiplier = 1f;
		[SerializeField, Tooltip(TT_elementWeights)] private Vector8 elementWeights = Vector8.One;
		[SerializeField, Tooltip(TT_sources)] private List<Source> sources;

		private Dictionary<string, Source> lookup;
		private readonly Source neutral = new Source();

		/// <summary>
		/// Returns the balancing data for <paramref name="source"/>, or a neutral entry if it has none configured.
		/// </summary>
		public Source GetSource(string source)
		{
			if (string.IsNullOrEmpty(source))
			{
				return neutral;
			}

			if (lookup == null)
			{
				lookup = new Dictionary<string, Source>();
				if (sources != null)
				{
					foreach (Source entry in sources)
					{
						if (!string.IsNullOrEmpty(entry.source))
						{
							lookup[entry.source] = entry;
						}
					}
				}
			}

			return lookup.TryGetValue(source, out Source found) ? found : neutral;
		}

		/// <summary>
		/// The base weight for <paramref name="element"/> (octad index).
		/// </summary>
		public float GetElementWeight(int element)
		{
			return elementWeights[element];
		}

		protected void OnValidate()
		{
			// Rebuild on the next request; the list may have been edited.
			lookup = null;
		}
	}
}
