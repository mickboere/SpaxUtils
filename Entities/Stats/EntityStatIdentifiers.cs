namespace SpaxUtils
{
	public class EntityStatIdentifiers : IStatIdentifierConstants
	{
		/// <summary>
		/// Default stat to show at the very top to make it clear a stat has not been selected yet.
		/// </summary>
		public const string NULL = "<NULL>";

		/// <summary>
		/// The timescale exclusive to this entity.
		/// </summary>
		public const string TIMESCALE = IStatIdentifierConstants.STATS + "Time Scale";
	}
}
