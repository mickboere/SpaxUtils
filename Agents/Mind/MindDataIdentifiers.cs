namespace SpaxUtils
{
	public class MindDataIdentifiers : ILabeledDataIdentifiers
	{
		private const string MIND = "MIND/";

		#region Inclination
		public const string INCLINATION = MIND + "Inclination"; // Stimulation weights

		public const string AGGRESSIVE = INCLINATION + "/Aggressive"; // Fire ->		Inclination to attack
		public const string PERCEPTIVE = INCLINATION + "/Perceptive"; // Light ->		Inclination to anticipate
		public const string EVASIVE = INCLINATION + "/Evasive"; // Air ->				Inclination to evade
		public const string SUPPORTIVE = INCLINATION + "/Supportive"; // Spirit ->		Inclination to support
		public const string APPREHENSIVE = INCLINATION + "/Apprehensive"; // Water ->	Inclination to retreat
		public const string INTUITIVE = INCLINATION + "/Intuitive"; // Nature ->		Inclination to adapt
		public const string DEFENSIVE = INCLINATION + "/Defensive"; // Earth ->			Inclination to guard
		public const string IMPASSIVE = INCLINATION + "/Impassive"; // Void ->			Inclination to target
		#endregion

		#region Personality
		public const string PERSONALITY = MIND + "Personality"; // Behavior weights

		public const string FIERCENESS = PERSONALITY + "/Fierceness"; // Fire ->		(meek > fierce)
		public const string SHARPNESS = PERSONALITY + "/Sharpness"; // Light ->			(dull > sharp)
		public const string SWIFTNESS = PERSONALITY + "/Swiftness"; // Air ->			(sluggish > swift)
		public const string KINDNESS = PERSONALITY + "/Kindness"; // Spirit ->			(cold > kind)
		public const string CAUTIOUSNESS = PERSONALITY + "/Cautiousness"; // Water ->	(rash > cautious)
		public const string ADAPTIVENESS = PERSONALITY + "/Adaptiveness"; // Nature ->	(rigid > adaptive)
		public const string STEADFASTNESS = PERSONALITY + "/Steadfastness"; // Earth ->	(fickle > steadfast)
		public const string RUTHLESSNESS = PERSONALITY + "/Ruthlessness"; // Void ->	(gentle > ruthless)
		#endregion
	}
}
