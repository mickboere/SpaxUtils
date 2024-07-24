namespace SpaxUtils
{
	public static class Vector8Extensions
	{
		/// <summary>
		/// Linearly interpolates between <paramref name="a"/> and <paramref name="b"/> by <paramref name="t"/>.
		/// </summary>
		public static Vector8 Lerp(this Vector8 a, Vector8 b, float t)
		{
			a = Vector8.Lerp(a, b, t);
			return a;
		}

		/// <summary>
		/// Linearly interpolates between <paramref name="a"/> and <paramref name="b"/> by <paramref name="t"/>, clamping <paramref name="t"/> bewteen 0 and 1.
		/// </summary>
		public static Vector8 LerpClamped(this Vector8 a, Vector8 b, float t)
		{
			a = Vector8.LerpClamped(a, b, t);
			return a;
		}

		/// <summary>
		/// Rotates <paramref name="v"/> by <paramref name="amount"/> members.
		/// </summary>
		/// <param name="v">The <see cref="Vector8"/> to rotate.</param>
		/// <param name="amount">The amount of members to rotate by (8 is a full rotation).</param>
		/// <returns><paramref name="v"/> rotated by <paramref name="amount"/> members.</returns>
		public static Vector8 Rotate(this Vector8 v, int amount)
		{
			v = Vector8.Rotate(v, amount);
			return v;
		}

		/// <summary>
		/// Travels from <paramref name="a"/> to <paramref name="b"/> taking steps of <paramref name="t"/>, never exceeding <paramref name="b"/>.
		/// </summary>
		public static Vector8 Travel(this Vector8 a, Vector8 b, float t)
		{
			a = Vector8.Travel(a, b, t);
			return a;
		}

		/// <summary>
		/// Clamps the members of <paramref name="v"/> between <paramref name="min"/> and <paramref name="max"/>.
		/// </summary>
		public static Vector8 Clamp(this Vector8 v, float min, float max)
		{
			v = Vector8.Clamp(v, min, max);
			return v;
		}

		/// <summary>
		/// Clamps the members of <paramref name="v"/> between 0 and 1.
		/// </summary>
		public static Vector8 Clamp01(this Vector8 v)
		{
			v = Vector8.Clamp01(v);
			return v;
		}

		/// <summary>
		/// Scales all of <paramref name="v"/>'s members proportionally so that its highest member never exceeds 1.
		/// Does NOT make it so that the total length of the vector is 1!
		/// </summary>
		public static Vector8 Normalize(this Vector8 v)
		{
			v = Vector8.Normalize(v);
			return v;
		}

		/// <summary>
		/// Makes all of <paramref name="v"/>'s members absolute values (turns all negatives into positives).
		/// </summary>
		public static Vector8 Absolute(this Vector8 v)
		{
			v = Vector8.Absolute(v);
			return v;
		}

		/// <summary>
		/// Returns the total distance between vectors <paramref name="a"/> and <paramref name="b"/>.
		/// </summary>
		public static float Distance(this Vector8 a, Vector8 b)
		{
			return Vector8.Distance(a, b);
		}

		/// <summary>
		/// Perform a "fluid" simulation on Vector8 <paramref name="v"/> where each member flows into its neighbours' weights <paramref name="w"/> multiplied by <paramref name="t"/>.
		/// </summary>
		/// <param name="v">The vector to perform the simulation on.</param>
		/// <param name="r">The rest position of the simulation.</param>
		/// <param name="w">The vector containing the simulation weights.</param>
		/// <param name="t">The "timestep".</param>
		/// <returns><paramref name="v"/> with a flow simulation applied.</returns>
		public static Vector8 Simulate(this Vector8 v, Vector8 r, Vector8 w, float t)
		{
			v = Vector8.Simulate(v, r, w, t);
			return v;
		}
	}
}
