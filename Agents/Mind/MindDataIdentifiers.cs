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

		public const string INTENSITY = PERSONALITY + "/Intensity"; // Fire -> Intensity
		public const string INTELLIGENCE = PERSONALITY + "/Intelligence"; // Light -> Anticipatory intelligence (0 is slow, 1 is sharp)
		public const string ENTHUSIASM = PERSONALITY + "/Enthusiasm"; // Air -> Speed
		public const string KINDNESS = PERSONALITY + "/Kindness"; // Faeth -> Goodness
		public const string SENSITIVITY = PERSONALITY + "/Sensitivity"; // Water -> Carefulness
		public const string INSTINCT = PERSONALITY + "/Instinct"; // Nature -> Opportunistic intelligence (0 is random, 1 is intuitive)
		public const string RESOLVE = PERSONALITY + "/Resolve"; // Earth -> Holding back
		public const string CRUELTY = PERSONALITY + "/Cruelty"; // Daeth -> Evil
		#endregion
	}
}
