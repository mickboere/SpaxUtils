using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// Maps one stat's value to another.
	/// </summary>
	[Serializable]
	public class StatMapping : IStatModConfig
	{
		public string FromStat => fromStat;
		public string ToStat => toSubStat ? toStat.SubStat(subStat) : toStat;
		public ModMethod Method => modMethod;
		public Operation Operation => operation;

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string fromStat;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string toStat;
		[SerializeField, HideInInspector] private bool toSubStat;
		[SerializeField, Conditional(nameof(toSubStat), drawToggle: true), ConstDropdown(typeof(ILabeledDataIdentifiers))] private string subStat;

		[SerializeField] private FormulaType formula;

		[SerializeField, Conditional(nameof(formula), 1)] private float expConstant = 0.1f;
		[SerializeField, Conditional(nameof(formula), 1)] private float expPower = 2f;

		[SerializeField, Conditional(nameof(formula), 2)] private float invExpConstant = 0.1f;
		[SerializeField, Conditional(nameof(formula), 2)] private float invExpPower = 2f;

		[SerializeField, Conditional(nameof(formula), 3)] private float logConstant = 0.1f;
		[SerializeField, Conditional(nameof(formula), 3)] private float logPower = 2f;
		[SerializeField, Conditional(nameof(formula), 3)] private float logShift = 0f;

		[SerializeField] private float scale = 1f;

		[SerializeField, Conditional(nameof(formula), 4)] private AnimationCurve curve;

		[Tooltip("Shifts the mapped value by x.")]
		[SerializeField] private float shift = 0f;

		[SerializeField] private ModMethod modMethod = ModMethod.Base;
		[SerializeField] private Operation operation = Operation.Set;

		private bool Round => scale > 1f;

		/// <inheritdoc/>
		public float GetModifierValue(float input)
		{
			switch (formula)
			{
				case FormulaType.Exp:
					return shift + SpaxFormulas.Exp(input, expConstant, expPower, Round);
				case FormulaType.InvExp:
					return shift + SpaxFormulas.InvExp(input, invExpConstant, invExpPower, Round);
				case FormulaType.Log:
					return shift + SpaxFormulas.Log(input, logConstant, logPower, scale, logShift, Round);
				case FormulaType.Curve:
					return shift + curve.Evaluate(input) * scale;
				default:
					return shift + input * scale;
			}
		}
	}
}
