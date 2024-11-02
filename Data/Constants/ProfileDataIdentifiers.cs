namespace SpaxUtils
{
	public class ProfileDataIdentifiers : ILabeledDataIdentifiers
	{
		private const string PROFILE = "PROFILE/";

		// Profile Configuration.
		public const string NAME = PROFILE + "Name";
		public const string SEED = PROFILE + "Seed";

		// Basic game data.
		public const string LAST_SPAWN = PROFILE + "LastSpawn";
	}
}
