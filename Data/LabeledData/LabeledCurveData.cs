using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledCurveData : ILabeledData
	{
		public string UID => identifier;
		public object Value { get { return curve; } set { curve = (AnimationCurve)value; } }
		public AnimationCurve CurveValue => curve;

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifierConstants))] private string identifier;
		[SerializeField] private AnimationCurve curve;

		public float Evaluate(float progress)
		{
			return curve.Evaluate(progress);
		}

		public float Evaluate(float time, float duration)
		{
			return Evaluate(time / duration);
		}
	}
}
