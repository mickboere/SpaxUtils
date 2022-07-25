using System;
using UnityEngine;

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

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifierConstants))] private string fromStat;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifierConstants))] private string toStat;
		[SerializeField] private bool useCurve;
		[SerializeField] private AnimationCurve curve;
		[SerializeField] private float outputMultiplier = 1f;
		[SerializeField] private ModMethod modMethod = ModMethod.Base;
		[SerializeField] private Operation operation = Operation.Set;

		/// <summary>
		/// Calculates the mapped value.
		/// </summary>
		public float GetMappedValue(float input)
		{
			if (useCurve)
			{
				return curve.Evaluate(input) * outputMultiplier;
			}
			else
			{
				return input * outputMultiplier;
			}
		}
	}
}
