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
		public bool Enabled => enabled;
		public string FromStat => fromStat;
		public bool SourceBase => sourceBase;
		public string ToStat => toSubStat ? toStat.SubStat(subStat) : toStat;
		public FormulaType Formula => formula;
		public ModMethod Method => modMethod;
		public Operation Operation => operation;

		[SerializeField] private bool enabled = true;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string fromStat;
		[SerializeField, Tooltip("Whether to source the FromStat's base value [TRUE], or to source its modded value [FALSE].")] private bool sourceBase;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string toStat;
		[SerializeField, HideInInspector] private bool toSubStat;
		[SerializeField, Conditional(nameof(toSubStat), hide: false, drawToggle: true), ConstDropdown(typeof(ILabeledDataIdentifiers), true)] private string subStat;

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

		[SerializeField, Conditional(nameof(formula), 11), Tooltip("The upper value approached but never reached.")] private float satCeiling = 1f;
		[SerializeField, Conditional(nameof(formula), 11), Tooltip("Input at which the output is half the ceiling.")] private float satHalf = 50f;
		[SerializeField, Conditional(nameof(formula), 11), Tooltip("Approach shape: 1 = steepest at 0, above 1 = S-curve, below 1 = sharper knee.")] private float satPower = 1f;

		[SerializeField] private float scale = 1f;

		[SerializeField, Tooltip("Adds to the final value.")] private float shift = 0f;

		[SerializeField] private ModMethod modMethod = ModMethod.Base;
		[SerializeField] private Operation operation = Operation.Set;

		private bool Round => scale > 1f;

		public StatMapping(string fromStat, bool sourceBase, string toStat, bool toSubStat = false, string subStat = "",
			FormulaType formula = FormulaType.Linear,
			float expPreScale = 1f, float expConstant = 0.1f, float expPower = 2f,
			float invExpPreScale = 1f, float invExpConstant = 0.1f, float invExpPower = 2f,
			float logConstant = 0.1f, float logPower = 2f, float logShift = 0f,
			AnimationCurve curve = null,
			Vector2 pointA = default, Vector2 pointB = default,
			float satCeiling = 1f, float satHalf = 50f, float satPower = 1f,
			float scale = 1f, float shift = 0f,
			ModMethod modMethod = ModMethod.Base, Operation operation = Operation.Set)
		{
			this.fromStat = fromStat;
			this.sourceBase = sourceBase;
			this.toStat = toStat;
			this.toSubStat = toSubStat;
			this.subStat = subStat;
			this.formula = formula;
			this.expPreScale = expPreScale;
			this.expConstant = expConstant;
			this.expPower = expPower;
			this.invExpPreScale = invExpPreScale;
			this.invExpConstant = invExpConstant;
			this.invExpPower = invExpPower;
			this.logConstant = logConstant;
			this.logPower = logPower;
			this.logShift = logShift;
			this.curve = curve;
			this.pointA = pointA;
			this.pointB = pointB;
			this.satCeiling = satCeiling;
			this.satHalf = satHalf;
			this.satPower = satPower;
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
					return shift + SpaxFormulas.Exp(input * expPreScale, expConstant, expPower, Round) * scale;
				case FormulaType.InvExp:
					return shift + SpaxFormulas.InvExp(input * invExpPreScale, invExpConstant, invExpPower, Round) * scale;
				case FormulaType.Log:
					return shift + SpaxFormulas.Log(input, logConstant, logPower, scale, logShift, Round);
				case FormulaType.Curve:
					return shift + curve.Evaluate(input) * scale;
				case FormulaType.Interpolate:
					return shift + pointA.y.Lerp(pointB.y, input.InverseLerp(pointA.x, pointB.x)) * scale;
				case FormulaType.Extrapolate:
					float y = (pointB.y - pointA.y) / (pointB.x - pointA.x);
					return shift + (pointA.y + (input - pointA.x) * y) * scale;
				case FormulaType.LevelToPhysic:
					return shift + SpaxFormulas.LevelToPhysic(input) * scale;
				case FormulaType.LevelToPointsStat:
					return shift + SpaxFormulas.LevelToPointsStat(input) * scale;
				case FormulaType.ExpToLevel:
					// No local constants: tracks SpaxFormulas.CONSTANT/POWER so the level curve can never
					// drift from the rank and level-up-cost curves.
					return shift + SpaxFormulas.LevelFromPoints(input) * scale;
				case FormulaType.ExpToRank:
					// Summed-octad EXP to rank; replaces InvExp with a 0.125 pre-scale.
					return shift + SpaxFormulas.RankFromPoints(input) * scale;
				case FormulaType.Saturate:
					// Ceiling is its own field; scale/shift stay pure post-ops, so the asymptote is shift + ceiling * scale.
					return shift + SpaxFormulas.Saturate(input, satCeiling, satHalf, satPower) * scale;
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
					// Reverse: Divide by Scale -> Invert Function -> Divide by PreScale
					return SpaxFormulas.InvExp(input / scale, expConstant, expPower, Round) / expPreScale;

				case FormulaType.InvExp:
					// Reverse: Divide by Scale -> Invert Function -> Divide by PreScale
					return SpaxFormulas.Exp(input / scale, invExpConstant, invExpPower, Round) / invExpPreScale;

				case FormulaType.Log:
					// Reverse: Use InvLog. 
					// Note: We pass 'input' (which is output - this.shift).
					// We pass 'logShift' as the shift, because SpaxFormulas.Log adds it internally.
					return SpaxFormulas.InvLog(input, logConstant, logPower, scale, logShift, Round);

				case FormulaType.Interpolate:
					float scaledInput = scale != 0f ? input / scale : 0f;
					float t = scaledInput.InverseLerp(pointA.y, pointB.y);
					return pointA.x.Lerp(pointB.x, t);

				case FormulaType.Extrapolate:
					float slopeNumerator = pointB.y - pointA.y;
					float slopeDenominator = pointB.x - pointA.x;

					if (Mathf.Abs(slopeNumerator) < 0.0001f) return 0f;

					float m = slopeNumerator / slopeDenominator;
					float unscaled = scale != 0f ? input / scale : 0f;
					return pointA.x + (unscaled - pointA.y) / m;

				case FormulaType.Linear:
					return scale != 0f ? input / scale : 0f;

				case FormulaType.LevelToPhysic:
					if (scale == 0f || SpaxFormulas.PHYSIC_SCALE == 0f) return 0f;
					return (input / scale - SpaxFormulas.PHYSIC_SHIFT) / SpaxFormulas.PHYSIC_SCALE;

				case FormulaType.LevelToPointsStat:
					if (scale == 0f || SpaxFormulas.POINTSSTAT_SCALE == 0f) return 0f;
					return (input / scale - SpaxFormulas.POINTSSTAT_SHIFT) / SpaxFormulas.POINTSSTAT_SCALE;

				case FormulaType.ExpToLevel:
					return SpaxFormulas.PointsFromLevel(scale != 0f ? input / scale : 0f);

				case FormulaType.ExpToRank:
					return SpaxFormulas.PointsFromRank(scale != 0f ? input / scale : 0f);

				case FormulaType.Saturate:
					return SpaxFormulas.InvSaturate(scale != 0f ? input / scale : 0f, satCeiling, satHalf, satPower);

				case FormulaType.Curve:
					SpaxDebug.Error($"Inverse modifier not supported for 'Curve' type (requires iterative solver). Calculation failed.");
					return 0f;

				default:
					SpaxDebug.Error($"Inverse modifier not supported for formula type: {formula}");
					return 0f;
			}
		}
	}
}
