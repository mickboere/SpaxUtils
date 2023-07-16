using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// Maps one stat's value to another.
	/// </summary>
	[Serializable]
	public class StatMapping
	{
		public string FromStat => fromStat;
		public string ToStat => toStat;
		public ModMethod ModMethod => modMethod;
		public Operation Operation => operation;

		private bool Round => scale > 99f;

		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string fromStat;
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string toStat;

		[SerializeField] private FormulaType formula;

		[SerializeField, Conditional(nameof(formula), 1)] private float expConstant = 0.1f;
		[SerializeField, Conditional(nameof(formula), 1)] private float expPower = 2f;

		[SerializeField, Conditional(nameof(formula), 2)] private float invExpConstant = 0.1f;
		[SerializeField, Conditional(nameof(formula), 2)] private float invExpPower = 2f;

		[SerializeField, Conditional(nameof(formula), 3)] private float logConstant = 0.1f;
		[SerializeField, Conditional(nameof(formula), 3)] private float logPower = 2f;
		[SerializeField, Conditional(nameof(formula), 3)] private float logShift = 0f;

		[SerializeField, FormerlySerializedAs("outputMultiplier")] private float scale = 1f;

		[SerializeField, Conditional(nameof(formula), 4)] private AnimationCurve curve;

		[Tooltip("Shifts the mapped value by x.")]
		[SerializeField] private float shift = 0f;

		[SerializeField] private ModMethod modMethod = ModMethod.Base;
		[SerializeField] private Operation operation = Operation.Set;

		/// <summary>
		/// Calculates the mapped value.
		/// </summary>
		public float GetMappedValue(float input)
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
