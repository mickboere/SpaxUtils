namespace SpaxUtils
{
	public class ProfileDataIdentifiers : ILabeledDataIdentifiers
	{
		private const string PROFILE = "PROFILE/";

		// Profile Configuration
		public const string NAME = PROFILE + "Name";
		public const string SEED = PROFILE + "Seed";

		// Timekeeping
		public const string LAST_SAVE = PROFILE + "LastSave";
		public const string INITIAL_TIME = PROFILE + "InitialTime";
		public const string PLAYTIME_UNSCALED = PROFILE + "UnscaledPlaytime";
		public const string PLAYTIME_SCALED = PROFILE + "ScaledPlaytime";
	}
}
