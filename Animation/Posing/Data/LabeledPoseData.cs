using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledPoseData : ILabeledDataProvider
	{
		/// <inheritdoc/>
		public IEnumerable<ILabeledData> LabeledData => new List<ILabeledData>().Concat(curveData).Concat(floatData).Concat(boolData);

		[SerializeField] private List<LabeledCurveData> curveData;
		[SerializeField] private List<LabeledFloatData> floatData;
		[SerializeField] private List<LabeledBoolData> boolData;

		/// <inheritdoc/>
		public bool TryGet<T>(string identifier, T defaultIfNull, out T result)
		{
			result = defaultIfNull;

			ILabeledData data = LabeledData.FirstOrDefault((d) => d.ID == identifier);
			if (data != null)
			{
				result = (T)data.Value;
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public bool TryGetFloat(string identifier, float defaultIfNull, out float result)
		{
			result = defaultIfNull;

			LabeledFloatData fData = floatData.FirstOrDefault((d) => d.ID == identifier);
			if (fData != null)
			{
				result = fData.FloatValue;
				return true;
			}

			LabeledCurveData cData = curveData.FirstOrDefault((d) => d.ID == identifier);
			if (cData != null)
			{
				result = cData.Evaluate(defaultIfNull);
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public bool TryGetInt(string identifier, int defaultIfNull, out int result)
		{
			result = defaultIfNull;

			// Since int's arent a thing in pose data yet, we'll check floats instead.
			if (TryGetFloat(identifier, defaultIfNull, out float fResult))
			{
				result = Mathf.RoundToInt(fResult);
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public bool TryGetBool(string identifier, bool defaultIfNull, out bool result)
		{
			LabeledBoolData data = boolData.FirstOrDefault((d) => d.ID == identifier);
			result = data != null ? data.BoolValue : defaultIfNull;
			return data != null;
		}

		/// <inheritdoc/>
		public bool TryGetString(string identifier, string defaultIfNull, out string result)
		{
			result = defaultIfNull;
			return false;
		}
	}
}
