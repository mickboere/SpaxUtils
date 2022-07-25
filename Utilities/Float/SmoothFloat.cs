namespace SpaxUtils
{
	/// <summary>
	/// <see cref="SmoothVarBase{T}"/> implementation that smooths out <see cref="float"/>s.
	/// </summary>
	public class SmoothFloat : SmoothVarBase<float>
	{
		public SmoothFloat(int capacity, bool weightFalloff = true) : base(capacity, weightFalloff) { }

		public override float GetValue()
		{
			float value = 0f;
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
