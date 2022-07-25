using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="SmoothVarBase{T}"/> implementation that smooths out <see cref="Vector3"/>s.
	/// </summary>
	public class SmoothVector3 : SmoothVarBase<Vector3>
	{
		public SmoothVector3(int capacity, bool weightFalloff = true) : base(capacity, weightFalloff) { }

		public override Vector3 GetValue()
		{
			Vector3 value = Vector3.zero;
			float count = 0f;

			for (int i = 0; i < stack.Count; i++)
			{
				float weight = weightFalloff ? (float)(i + 1) / stack.Count : 1f;
				value += stack[i] * weight;
				count += weight;
			}

			return value / count;
		}
	}
}
