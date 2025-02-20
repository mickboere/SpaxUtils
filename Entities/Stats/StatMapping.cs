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

		[SerializeField, Conditional(nameof(formula), 4)] private AnimationCurve curve;

		[SerializeField, Conditional(nameof(formula), enumValues: new int[] { 5, 6 })] private Vector2 pointA;
		[SerializeField, Conditional(nameof(formula), enumValues: new int[] { 5, 6 })] private Vector2 pointB;

		[SerializeField] private float scale = 1f;

		[SerializeField, Tooltip("Adds to the final value.")] private float shift = 0f;

		[SerializeField] private ModMethod modMethod = ModMethod.Base;
		[SerializeField] private Operation operation = Operation.Set;

		private bool Round => scale > 1f;

		/// <inheritdoc/>
		public float GetModifierValue(float input)
		{
			return GetModifierValue(input, formula);
		}

		public float GetInverseModifierValue(float output)
		{
			switch (formula)
			{
				case FormulaType.Exp:
					return GetModifierValue(output, FormulaType.InvExp);
				case FormulaType.InvExp:
					return GetModifierValue(output, FormulaType.Exp);
				default:
					SpaxDebug.Error($"Inverse modifier not supported for: {formula}");
					return 0f;
			}
		}

		private float GetModifierValue(float input, FormulaType formula)
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
				case FormulaType.Interpolate:
					return shift + pointA.y.Lerp(pointB.y, input.InverseLerp(pointA.x, pointB.x)) * scale;
				case FormulaType.Extrapolate:
					float y = (pointB.y - pointA.y) / (pointB.x - pointA.x);
					return shift + pointA.y + (input - pointA.x) * y;
				default:
					return shift + input * scale;
			}
		}
	}
}
