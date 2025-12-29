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
		public bool SourceBase => sourceBase;
		public string ToStat => toSubStat ? toStat.SubStat(subStat) : toStat;
		public FormulaType Formula => formula;
		public ModMethod Method => modMethod;
		public Operation Operation => operation;

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string fromStat;
		[SerializeField, Tooltip("Whether to source the FromStat's base value [TRUE], or to source its modded value [FALSE].")] private bool sourceBase;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string toStat;
		[SerializeField, HideInInspector] private bool toSubStat;
		[SerializeField, Conditional(nameof(toSubStat), hide: false, drawToggle: true), ConstDropdown(typeof(ILabeledDataIdentifiers), true)] private string subStat;

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

		public StatMapping(string fromStat, bool sourceBase, string toStat, bool toSubStat = false, string subStat = "",
			FormulaType formula = FormulaType.Linear,
			float expConstant = 0.1f, float expPower = 2f,
			float invExpConstant = 0.1f, float invExpPower = 2f,
			float logConstant = 0.1f, float logPower = 2f, float logShift = 0f,
			AnimationCurve curve = null,
			Vector2 pointA = default, Vector2 pointB = default,
			float scale = 1f, float shift = 0f,
			ModMethod modMethod = ModMethod.Base, Operation operation = Operation.Set)
		{
			this.fromStat = fromStat;
			this.sourceBase = sourceBase;
			this.toStat = toStat;
			this.toSubStat = toSubStat;
			this.subStat = subStat;
			this.formula = formula;
			this.expConstant = expConstant;
			this.expPower = expPower;
			this.invExpConstant = invExpConstant;
			this.invExpPower = invExpPower;
			this.logConstant = logConstant;
			this.logPower = logPower;
			this.logShift = logShift;
			this.curve = curve;
			this.pointA = pointA;
			this.pointB = pointB;
			this.scale = scale;
			this.shift = shift;
			this.modMethod = modMethod;
			this.operation = operation;
		}

		/// <inheritdoc/>
		public float GetModifierValue(float input)
		{
			return GetModifierValue(input, formula);
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
				case FormulaType.Default_LevelToPoints:
				default:
					return shift + input * scale;
			}
		}

		public float GetInverseModifierValue(float output)
		{
			// 1. Handle Shift First
			// The inverse of "Value + Shift" is "Value - Shift"
			float input = output - shift;

			switch (formula)
			{
				case FormulaType.Exp:
					// We are inverting an Exponential formula.
					// We use the InvExp math, but we MUST use the 'exp' constants that defined the curve.
					return SpaxFormulas.InvExp(input, expConstant, expPower, Round);

				case FormulaType.InvExp:
					// We are inverting an Inverse-Exponential formula (turning it back into Exp).
					// We use the Exp math, but we MUST use the 'invExp' constants that defined the curve.
					return SpaxFormulas.Exp(input, invExpConstant, invExpPower, Round);

				default:
					// Strict error for all other types (Linear, Log, Curve, etc.)
					SpaxDebug.Error($"Inverse modifier not supported for formula type: {formula}");
					return 0f;
			}
		}
	}
}
