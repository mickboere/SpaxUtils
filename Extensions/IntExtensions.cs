namespace SpaxUtils
{
	public static class IntExtensions
	{
		/// <summary>
		/// i % max; "repeat" <paramref name="i"/> never exceeding <paramref name="max"/>.
		/// </summary>
		public static int Repeat(this int i, int max = 1)
		{
			return i % max;
		}

		/// <summary>
		/// Add <paramref name="add"/> to <paramref name="i"/> and repeat as to never exceed <paramref name="max"/>.
		/// </summary>
		public static int AddAndRepeat(this int i, int add, int max = 1)
		{
			return (i + add) % max;
		}
	}
}
