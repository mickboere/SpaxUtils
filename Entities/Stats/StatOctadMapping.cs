using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="StatMapping"/> helper class that allows for easy configuration of 8 identical stat mappings at once.
	/// </summary>
	[Serializable]
	public class StatOctadMapping
	{
		[SerializeField, HideInInspector] private bool fromSingleStat;
		[SerializeField, Conditional(nameof(fromSingleStat), hide: false, drawToggle: true), ConstDropdown(typeof(ILabeledDataIdentifiers), true)] private string fromSingle;
		[SerializeField, Conditional(nameof(fromSingleStat), true), Expandable] private StatOctadAsset fromStatOctad;
		[SerializeField, Tooltip("Whether to source the FromStat's base value [TRUE], or to source its modded value [FALSE].")] private bool sourceBase;
		[SerializeField, HideInInspector] private bool toSingleStat;
		[SerializeField, Conditional(nameof(toSingleStat), hide: false, drawToggle: true), ConstDropdown(typeof(ILabeledDataIdentifiers), true)] private string toSingle;
		[SerializeField, Conditional(nameof(toSingleStat), true), Expandable] private StatOctadAsset toStatOctad;
		[SerializeField, HideInInspector] private bool toSubStats;
		[SerializeField, Conditional(nameof(toSubStats), hide: false, drawToggle: true), ConstDropdown(typeof(ILabeledDataIdentifiers), true)] private string subStat;

		[SerializeField] private FormulaType formula;

		[SerializeField, Conditional(nameof(formula), 1)] private float expPreScale = 1f;
		[SerializeField, Conditional(nameof(formula), 1)] private float expConstant = 0.1f;
		[SerializeField, Conditional(nameof(formula), 1)] private float expPower = 2f;

		[SerializeField, Conditional(nameof(formula), 2)] private float invExpPreScale = 1f;
		[SerializeField, Conditional(nameof(formula), 2)] private float invExpConstant = 0.1f;
		[SerializeField, Conditional(nameof(formula), 2)] private float invExpPower = 2f;

		[SerializeField, Conditional(nameof(formula), 3)] private float logConstant = 0.1f;
		[SerializeField, Conditional(nameof(formula), 3)] private float logPower = 2f;
		[SerializeField, Conditional(nameof(formula), 3)] private float logShift = 0f;

		[SerializeField, Conditional(nameof(formula), 4)] private AnimationCurve curve;

		[SerializeField, Conditional(nameof(formula), enumValues: new int[] { 5, 6 })] private Vector2 pointA;
		[SerializeField, Conditional(nameof(formula), enumValues: new int[] { 5, 6 })] private Vector2 pointB;

		[SerializeField] private float scale = 1f;

		[SerializeField, Tooltip("Adds to the final value.")] private float shift = 0f;

		[SerializeField] private ModMethod modMethod = ModMethod.Base;
		[SerializeField] private Operation operation = Operation.Set;

		/// <summary>
		/// Converts the 8 octad stats into individual StatMappings.
		/// </summary>
		public StatMapping[] GetMappings()
		{
			StatMapping[] mappings = new StatMapping[8];
			for (int i = 0; i < 8; i++)
			{
				string fromStat = fromSingleStat ? fromSingle : fromStatOctad.StatOctad.GetIdentifier(i);
				string toStat = toSingleStat ? toSingle : toStatOctad.StatOctad.GetIdentifier(i);
				mappings[i] = new StatMapping(
					fromStat, sourceBase, toStat, toSubStats, subStat,
					formula,
					expPreScale, expConstant, expPower, // Updated
					invExpPreScale, invExpConstant, invExpPower, // Updated
					logConstant, logPower, logShift,
					curve,
					pointA, pointB,
					scale, shift,
					modMethod, operation);
			}
			return mappings;
		}
	}
}
