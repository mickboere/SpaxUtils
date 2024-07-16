namespace SpaxUtils
{
	/// <summary>
	/// All the base attributes and stats used in Spirit Axis.
	/// </summary>
	public class AgentStatIdentifiers : IStatIdentifierConstants
	{
		/// <summary>
		/// Default stat to show at the very top to make it clear a stat has not been selected yet.
		/// </summary>
		public const string NULL = "<NULL>";

		// < Attributes >
		// Attributes are measured in EXP and converted to LEVELS.
		// Each Attribute maps to one or more STATS.

		private const string ATTRIBUTES = "ATTRIBUTES/";

		// Body Attributes
		#region Body Attributes
		private const string BODY_ATTRIBUTE = ATTRIBUTES + "BODY/";
		public const string BODY_EXPERIENCE = BODY_ATTRIBUTE + "Body_Experience"; // All atributes combined.
		public const string BODY_LEVEL = BODY_ATTRIBUTE + "Body_Level"; // All attribute levels combined.
		// Earth
		public const string TENACITY = BODY_ATTRIBUTE + "Tenacity";
		public const string TENACITY_LEVEL = BODY_ATTRIBUTE + "Tenacity_Level";
		// Daeth
		public const string HOSTILITY = BODY_ATTRIBUTE + "Hostility";
		public const string HOSTILITY_LEVEL = BODY_ATTRIBUTE + "Hostility_Level";
		// Fire
		public const string ACTIVITY = BODY_ATTRIBUTE + "Activity";
		public const string ACTIVITY_LEVEL = BODY_ATTRIBUTE + "Activity_Level";
		// Light
		public const string DEXTERITY = BODY_ATTRIBUTE + "Dexterity";
		public const string DEXTERITY_LEVEL = BODY_ATTRIBUTE + "Dexterity_Level";
		// Air
		public const string AGILITY = BODY_ATTRIBUTE + "Agility";
		public const string AGILITY_LEVEL = BODY_ATTRIBUTE + "Agility_Level";
		// Faeth
		public const string IMMUNITY = BODY_ATTRIBUTE + "Immunity";
		public const string IMMUNITY_LEVEL = BODY_ATTRIBUTE + "Immunity_Level";
		// Water
		public const string CAPACITY = BODY_ATTRIBUTE + "Capacity";
		public const string CAPACITY_LEVEL = BODY_ATTRIBUTE + "Capacity_Level";
		// Nature
		public const string VITALITY = BODY_ATTRIBUTE + "Vitality";
		public const string VITALITY_LEVEL = BODY_ATTRIBUTE + "Vitality_Level";
		#endregion Body Attributes

		// Mind Attributes
		#region Mind Attributes
		private const string MIND_ATTRIBUTE = ATTRIBUTES + "MIND/";
		public const string DEFENSIVE = MIND_ATTRIBUTE + "Defensive"; // Earth -> Likeliness to guard
		public const string IMPASSIVE = MIND_ATTRIBUTE + "Impassive"; // Daeth -> Likeliness to hostility
		public const string AGGRESSIVE = MIND_ATTRIBUTE + "Aggressive"; // Fire -> Likeliness to attack
		public const string PERCEPTIVE = MIND_ATTRIBUTE + "Perceptive"; // Light -> Likeliness to anticipate
		public const string EVASIVE = MIND_ATTRIBUTE + "Evasive"; // Air -> Likeliness to evade
		public const string SUPPORTIVE = MIND_ATTRIBUTE + "Supportive"; // Faeth -> Likeliness to support
		public const string APPREHENSIVE = MIND_ATTRIBUTE + "Apprehensive"; // Water -> Likeliness to keep distance
		public const string INTUITIVE = MIND_ATTRIBUTE + "Intuitive"; // Nature -> Likeliness to seek advantage
		#endregion

		// Soul Attributes
		#region Soul Attributes
		private const string SOUL_ATTRIBUTE = ATTRIBUTES + "SOUL/";
		public const string SPIRIT = SOUL_ATTRIBUTE + "Spirit"; // Spendable soul experience points.
		public const string VIRTUE = SOUL_ATTRIBUTE + "Virtue"; // Total amount of virtuous spirit gained.
		public const string SIN = SOUL_ATTRIBUTE + "Sin"; // Total amount of sinful spirit gained.
		public const string SOUL_LEVEL = SOUL_ATTRIBUTE + "Soul_Level"; // All soul attributes combined.
		// Attributes
		public const string GRAVITY = SOUL_ATTRIBUTE + "Gravity"; // Make heavy
		public const string AVIDITY = SOUL_ATTRIBUTE + "Avidity"; // Experience gain
		public const string INTENSITY = SOUL_ATTRIBUTE + "Intensity"; // Magic Power
		public const string FACILITY = SOUL_ATTRIBUTE + "Facility"; // Magic Casting Speed
		public const string LEVITY = SOUL_ATTRIBUTE + "Levity"; // Make light
		public const string PURITY = SOUL_ATTRIBUTE + "Purity"; // Luck
		public const string SENSITIVITY = SOUL_ATTRIBUTE + "Sensitivity"; // Magic Efficiency
		public const string CREATIVITY = SOUL_ATTRIBUTE + "Creativity"; // Recovery Speed
		#endregion Soul Attributes

		// < Stats >
		// Stats are measured in Points.

		// Body stats
		#region Body Stats
		private const string BODY_STAT = IStatIdentifierConstants.STATS + "BODY/";

		// Earth
		public const string MASS = BODY_STAT + "Mass"; // Total body mass.
		public const string LOAD = BODY_STAT + "Load"; // Total equip load.
		public const string ENDURANCE = BODY_STAT + "Endurance"; // Total amount of force one can absorb.
		public const string DEFENCE = BODY_STAT + "Defence"; // Total amount of passive defence.
		public const string GUARD = BODY_STAT + "Guard"; // Total amount of active (guarding) defence.
		// Daeth
		public const string OFFENCE = BODY_STAT + "Offence"; // Total damage output.
		public const string PIERCING = BODY_STAT + "Piercing"; // Piercing power of attacks, scales with Offence to calculate penetration.
		// Fire
		public const string ENERGY = BODY_STAT + "Energy"; // Amount of available force.
		public const string STRENGTH = BODY_STAT + "Strength"; // Force output.
		// Light
		public const string STATIC = BODY_STAT + "Static"; // Amount of available charge.
		public const string ATTACK_CHARGE_SPEED = BODY_STAT + "Attack_Charge_Speed";
		public const string ATTACK_PERFORM_SPEED = BODY_STAT + "Attack_Perform_Speed";
		// Air
		public const string STAMINA = BODY_STAT + "Stamina";
		public const string MOVEMENT_SPEED = BODY_STAT + "Movement_Speed";
		// Faeth
		public const string FRAILTY = BODY_STAT + "Frailty";
		// Water
		public const string MANA = BODY_STAT + "Mana";
		// Growth
		public const string HEALTH = BODY_STAT + "Health";
		public const string RECOVERY = BODY_STAT + "Recovery";
		public const string RECOVERY_DELAY = BODY_STAT + "Recovery_Delay";
		#endregion Body Stats

		// Soul Stats
		//private const string SOUL_STAT = IStatIdentifierConstants.STATS + "SOUL/";

		#region Sub Stats

		/// <summary>
		/// <see cref="StringExtensions.SubStat(string, string)"/>.
		/// </summary>
		public const string SUB_STAT = IStatIdentifierConstants.STATS + "SUB/";

		// Point-stats
		public const string SUB_MAX = SUB_STAT + "Max";
		public const string SUB_RECOVERABLE = SUB_STAT + "Recoverable";
		public const string SUB_FRAILTY = SUB_STAT + "Frailty";
		public const string SUB_RECOVERY = SUB_STAT + "Recovery";
		public const string SUB_RECOVERY_DELAY = SUB_STAT + "Recovery_Delay";
		public const string SUB_COST = SUB_STAT + "Cost";

		// Limbs
		public const string SUB_RIGHT_HAND = SUB_STAT + "Right Hand";
		public const string SUB_LEFT_HAND = SUB_STAT + "Left Hand";

		#endregion Sub Stats
	}
}
