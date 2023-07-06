namespace SpaxUtils
{
	/// <summary>
	/// All the base attributes and stats used in Spirit Axis.
	/// </summary>
	public class AgentStatIdentifiers : IStatIdentifierConstants
	{
		// < Attributes >
		// Attributes are measured in EXP and converted to LEVELS.
		// Each Attribute maps to one or more STATS.

		private const string ATTRIBUTES = "ATTRIBUTES/";
		private const string BODY_ATTRIBUTE = ATTRIBUTES + "BODY/";
		private const string SOUL_ATTRIBUTE = ATTRIBUTES + "SOUL/";

		#region Body Attributes

		public const string TENACITY = BODY_ATTRIBUTE + "Tenacity"; // Endurance
		public const string TENACITY_LEVEL = BODY_ATTRIBUTE + "Tenacity Level";

		public const string HOSTILITY = BODY_ATTRIBUTE + "Hostility"; // Damage
		public const string HOSTILITY_LEVEL = BODY_ATTRIBUTE + "Hostility Level";

		public const string ACTIVITY = BODY_ATTRIBUTE + "Activity"; // Energy
		public const string ACTIVITY_LEVEL = BODY_ATTRIBUTE + "Activity Level";

		public const string DEXTERITY = BODY_ATTRIBUTE + "Dexterity"; // Attack speed
		public const string DEXTERITY_LEVEL = BODY_ATTRIBUTE + "Dexterity Level";

		public const string AGILITY = BODY_ATTRIBUTE + "Agility"; // Movement speed
		public const string AGILITY_LEVEL = BODY_ATTRIBUTE + "Agility Level";

		public const string PURITY = BODY_ATTRIBUTE + "Purity"; // Luck
		public const string PURITY_LEVEL = BODY_ATTRIBUTE + "Purity Level";

		public const string CAPACITY = BODY_ATTRIBUTE + "Capacity"; // Mana
		public const string CAPACITY_LEVEL = BODY_ATTRIBUTE + "Capacity Level";

		public const string VITALITY = BODY_ATTRIBUTE + "Vitality"; // Health
		public const string VITALITY_LEVEL = BODY_ATTRIBUTE + "Vitality Level";

		#endregion Body Attributes

		#region Soul Attributes

		public const string INTENSITY = SOUL_ATTRIBUTE + "Intensity"; // Strength
		public const string SENSITIVITY = SOUL_ATTRIBUTE + "Sensitivity"; // Magic
		public const string GRAVITY = SOUL_ATTRIBUTE + "Gravity"; // Make heavy
		public const string LEVITY = SOUL_ATTRIBUTE + "Levity"; // Make light
		public const string FACILITY = SOUL_ATTRIBUTE + "Facility"; // Casting speed
		public const string RECOVERY = SOUL_ATTRIBUTE + "Recovery"; // Recovery
		public const string AVIDITY = SOUL_ATTRIBUTE + "Avidity"; // Experience gain
		public const string IMMUNITY = SOUL_ATTRIBUTE + "Immunity"; // Resistance

		#endregion Soul Attributes

		// < Stats >
		// Stats are measured in Points.

		private const string BODY_STAT = IStatIdentifierConstants.STATS + "BODY/";
		private const string SOUL_STAT = IStatIdentifierConstants.STATS + "SOUL/";

		#region Body Stats

		// Energy
		public const string ENERGY = BODY_STAT + "Energy";
		public const string ENERGY_MAX = BODY_STAT + "Max Energy";
		public const string ENERGY_RECOVERABLE = BODY_STAT + "Recoverable Energy";
		public const string ENERGY_RECOVERY = BODY_STAT + "Energy Recovery";

		// Mana
		public const string MANA = BODY_STAT + "Mana";
		public const string MANA_MAX = BODY_STAT + "Max Mana";
		public const string MANA_RECOVERABLE = BODY_STAT + "Recoverable Mana";
		public const string MANA_RECOVERY = BODY_STAT + "Mana Recovery";

		// Endurance
		public const string ENDURANCE = BODY_STAT + "Endurance";
		public const string ENDURANCE_MAX = BODY_STAT + "Max Endurance";
		public const string ENDURANCE_RECOVERABLE = BODY_STAT + "Recoverable Endurance";
		public const string ENDURANCE_RECOVERY = BODY_STAT + "Endurance Recovery";

		// Movement Speed
		public const string MOVEMENT_SPEED = BODY_STAT + "Movement Speed";

		// Attack Speed
		public const string ATTACK_CHARGE_SPEED = BODY_STAT + "Attack Charge Speed";
		public const string ATTACK_PERFORM_SPEED = BODY_STAT + "Attack Perform Speed";

		// Health
		public const string HEALTH = BODY_STAT + "Health";
		public const string HEALTH_MAX = BODY_STAT + "Max Health";
		public const string HEALTH_RECOVERABLE = BODY_STAT + "Recoverable Health";
		public const string HEALTH_RECOVERY = BODY_STAT + "Health Recovery";

		// Damage
		public const string DAMAGE = BODY_STAT + "Damage";

		// Luck
		public const string LUCK = BODY_STAT + "Luck";

		#endregion Body Stats

		#region Soul Stats

		public const string SPIRIT = SOUL_STAT + "Spirit";

		#endregion Soul Stats
	}
}
