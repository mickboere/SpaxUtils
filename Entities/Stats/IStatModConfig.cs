namespace SpaxUtils
{
	public interface IStatModConfig
	{
		public ModMethod Method { get; }
		public Operation Operation { get; }

		/// <summary>
		/// Calculates the stat modifier's value.
		/// </summary>
		/// <param name="modStat">The value of the stat modifier.</param>
		/// <returns>The final mod value to operate on.</returns>
		public float GetModifierValue(float modStat);
	}
}
