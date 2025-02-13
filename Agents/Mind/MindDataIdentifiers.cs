namespace SpaxUtils
{
	public class MindDataIdentifiers : ILabeledDataIdentifiers
	{
		private const string MIND = "MIND/";

		#region Inclination
		public const string INCLINATION = MIND + "Inclination"; // Stimulation weights

		public const string AGGRESSIVE = INCLINATION + "/Aggressive"; // Fire -> Inclination to attack
		public const string PERCEPTIVE = INCLINATION + "/Perceptive"; // Light -> Inclination to anticipate
		public const string EVASIVE = INCLINATION + "/Evasive"; // Air -> Inclination to evade
		public const string SUPPORTIVE = INCLINATION + "/Supportive"; // Faeth -> Inclination to support
		public const string APPREHENSIVE = INCLINATION + "/Apprehensive"; // Water -> Inclination to keep distance
		public const string INTUITIVE = INCLINATION + "/Intuitive"; // Nature -> Inclination to seek advantage
		public const string DEFENSIVE = INCLINATION + "/Defensive"; // Earth -> Inclination to guard
		public const string IMPASSIVE = INCLINATION + "/Impassive"; // Daeth -> Inclination to hostility
		#endregion

		#region Personality
		public const string PERSONALITY = MIND + "Personality"; // Sub-behaviour weights

		public const string FIERCENESS = PERSONALITY + "/Fierceness"; // Fire ->	(tired > fierce)
		public const string SHARPNESS = PERSONALITY + "/Sharpness"; // Light ->		(slow > sharp)
		public const string SWIFTNESS = PERSONALITY + "/Swiftness"; // Air ->		(easy > swift)
		public const string KINDNESS = PERSONALITY + "/Kindness"; // Faeth ->		(selfish > caring)
		public const string CAREFULNESS = PERSONALITY + "/Carefulness"; // Water ->	(careless > careful)
		public const string APTNESS = PERSONALITY + "/Aptness"; // Nature ->		(random > relevant)
		public const string SERIOUSNESS = PERSONALITY + "/Seriousness"; // Earth ->	(soft > stern)
		public const string BITTERNESS = PERSONALITY + "/Bitterness"; // Daeth ->	(tolerable > resentful)
		#endregion
	}
}
