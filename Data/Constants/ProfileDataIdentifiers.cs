namespace SpaxUtils
{
	public class ProfileDataIdentifiers : ILabeledDataIdentifiers
	{
		private const string PROFILE = "PROFILE/";

		// Profile Configuration.
		public const string NAME = PROFILE + "Name";
		public const string SEED = PROFILE + "Seed";
		public const string CYCLE = PROFILE + "Cycle";

		// Basic game data.
		public const string LAST_SPAWN = PROFILE + "LastSpawn";

		// Per-save settings overrides.
		public const string SETTINGS = PROFILE + "Settings";
	}
}
