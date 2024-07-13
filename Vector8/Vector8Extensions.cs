namespace SpaxUtils
{
	public static class Vector8Extensions
	{
		/// <summary>
		/// <see cref="Vector8.Lerp(Vector8, Vector8, float)"/>.
		/// </summary>
		public static Vector8 Lerp(this Vector8 a, Vector8 b, float t)
		{
			a = Vector8.Lerp(a, b, t);
			return a;
		}

		/// <summary>
		/// <see cref="Vector8.LerpClamped(Vector8, Vector8, float)"/>.
		/// </summary>
		public static Vector8 LerpClamped(this Vector8 a, Vector8 b, float t)
		{
			a = Vector8.LerpClamped(a, b, t);
			return a;
		}

		/// <summary>
		/// <see cref="Vector8.Rotate(Vector8, int)"/>.
		/// </summary>
		public static Vector8 Rotate(this Vector8 v, int amount)
		{
			v = Vector8.Rotate(v, amount);
			return v;
		}

		/// <summary>
		/// <see cref="Vector8.Travel(Vector8, Vector8, float)"/>.
		/// </summary>
		public static Vector8 Travel(this Vector8 a, Vector8 b, float t)
		{
			a = Vector8.Travel(a, b, t);
			return a;
		}

		/// <summary>
		/// <see cref="Vector8.Simulate(Vector8, Vector8, float)"/>.
		/// </summary>
		public static Vector8 Simulate(this Vector8 v, Vector8 r, Vector8 w, float t)
		{
			v = Vector8.Simulate(v, r, w, t);
			return v;
		}
	}
}
