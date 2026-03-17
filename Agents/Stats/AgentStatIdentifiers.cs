namespace SpaxUtils
{
	/// <summary>
	/// All the base attributes and stats used in Spirit Axis agents.
	/// </summary>
	public class AgentStatIdentifiers : IStatIdentifiers
	{
		// < Attributes >
		// Attributes are measured in EXP and converted to LEVELS.
		// Each Attribute LEVEL maps to one or more STATS.

		private const string ATTRIBUTES = "ATTRIBUTES/";

		// BODILY ATTRIBUTES
		#region Bodily Attributes
		private const string BODY_ATTRIBUTE = ATTRIBUTES + "BODY/"; // Bodily attributes category.

		public const string BODY_EXPERIENCE = BODY_ATTRIBUTE + "Experience"; // Sum of all atribute experiences.
		private const string BODY_EXP = BODY_ATTRIBUTE + "EXP/"; // Body experiences category.

		public const string BODY_LEVEL = BODY_ATTRIBUTE + "Level"; // Sum of all bodily attribute levels.
		public const string BODY_RANK = BODY_ATTRIBUTE + "Rank"; // Average of all body attribute levels.
		private const string BODY_LVL = BODY_ATTRIBUTE + "LVL/"; // Body levels category.

		public const string BODY_DISTRIBUTION = BODY_ATTRIBUTE + "Distribution"; // Normalized body attribute distribution key.

		// Fire
		public const string FEROCITY_EXP = BODY_EXP + "Ferocity";
		public const string FEROCITY_LVL = BODY_LVL + "Ferocity";
		// Light
		public const string ACUITY_EXP = BODY_EXP + "Acuity";
		public const string ACUITY_LVL = BODY_LVL + "Acuity";
		// Air
		public const string AGILITY_EXP = BODY_EXP + "Agility";
		public const string AGILITY_LVL = BODY_LVL + "Agility";
		// Spirit
		public const string IMMUNITY_EXP = BODY_EXP + "Immunity";
		public const string IMMUNITY_LVL = BODY_LVL + "Immunity";
		// Water
		public const string CAPACITY_EXP = BODY_EXP + "Capacity";
		public const string CAPACITY_LVL = BODY_LVL + "Capacity";
		// Nature
		public const string VITALITY_EXP = BODY_EXP + "Vitality";
		public const string VITALITY_LVL = BODY_LVL + "Vitality";
		// Earth
		public const string INTEGRITY_EXP = BODY_EXP + "Integrity";
		public const string INTEGRITY_LVL = BODY_LVL + "Integrity";
		// Void
		public const string LETHALITY_EXP = BODY_EXP + "Lethality";
		public const string LETHALITY_LVL = BODY_LVL + "Lethality";
		#endregion Bodily Attributes

		// SOUL ATTRIBUTES
		#region Soul Attributes
		private const string SOUL_ATTRIBUTE = ATTRIBUTES + "SOUL/";

		public const string SOUL_EXPERIENCE = SOUL_ATTRIBUTE + "Experience"; // All soul attributes combined.
		private const string SOUL_EXP = SOUL_ATTRIBUTE + "EXP/"; // 

		public const string SOUL_LEVEL = SOUL_ATTRIBUTE + "Level"; // Sum of all soul attribute levels.
		public const string SOUL_RANK = SOUL_ATTRIBUTE + "Rank"; // Average of all soul attribute levels.
		private const string SOUL_LVL = SOUL_ATTRIBUTE + "LVL/"; // 

		public const string SOUL_DISTRIBUTION = SOUL_ATTRIBUTE + "Distribution"; // Normalized soul attribute distribution key.

		// Attributes
		// Fire
		public const string INTENSITY_EXP = SOUL_EXP + "Intensity"; // Increases base impact % and weapon strength curve.
		public const string INTENSITY_LVL = SOUL_LVL + "Intensity";
		// Light
		public const string FACILITY_EXP = SOUL_EXP + "Facility"; // Increases overcharge efficiency.
		public const string FACILITY_LVL = SOUL_LVL + "Facility";
		// Air
		public const string LEVITY_EXP = SOUL_EXP + "Levity"; // Make light; improves aerial control, jump height, fall speed, fine control of spells and movement.
		public const string LEVITY_LVL = SOUL_LVL + "Levity";
		// Spirit
		public const string PURITY_EXP = SOUL_EXP + "Purity"; // Improves luck and resistance to negative effects.
		public const string PURITY_LVL = SOUL_LVL + "Purity";
		// Water
		public const string SENSITIVITY_EXP = SOUL_EXP + "Sensitivity"; // Improves mana efficiency and proficiency curve.
		public const string SENSITIVITY_LVL = SOUL_LVL + "Sensitivity";
		// Nature
		public const string FECUNDITY_EXP = SOUL_EXP + "Fecundity"; // Improves recovery of all point-stats.
		public const string FECUNDITY_LVL = SOUL_LVL + "Fecundity";
		// Earth
		public const string GRAVITY_EXP = SOUL_EXP + "Gravity"; // Make heavy; improves knockback resistance and effectiveness of heavy forces.
		public const string GRAVITY_LVL = SOUL_LVL + "Gravity";
		// Void
		public const string HOSTILITY_EXP = SOUL_EXP + "Hostility"; // Raises combat danger and experience gain.
		public const string HOSTILITY_LVL = SOUL_LVL + "Hostility";
		#endregion Soul Attributes

		// < Stats >
		// Stats are either measured in Points (PointStats & Physics), percentages (0..1) or in real values (kg, m/s, etc).

		// MIND STATS
		#region Mind Stats

		private const string MIND_STAT = IStatIdentifiers.STATS + "MIND/";

		// General
		private const string MIND_GENERAL = MIND_STAT + "GENERAL/";
		public const string AGGRO = MIND_STAT + "Aggro"; // Current aggro level.

		#endregion Mind Stats

		// BODILY STATS
		#region Bodily Stats

		private const string BODY_STAT = IStatIdentifiers.STATS + "BODY/";

		// GENERAL
		private const string BODY_GENERAL = BODY_STAT + "GENERAL/";
		public const string MASS = BODY_GENERAL + "Mass"; // Total body mass in KG.
		public const string LOAD = BODY_GENERAL + "Load"; // Total equip load in KG.
		public const string HARDNESS = BODY_GENERAL + "Hardness"; // Hardness of the body (0-1), used in calculating impacts.
		public const string VULNERABILITY = BODY_GENERAL + "Vulnerability"; // Vulnerability of the body (0-1), used in calculating damage.
		public const string REACH = BODY_GENERAL + "Reach"; // The agent's base melee reach (should be as large as the idle collision radius, limbs define actual reach).
		public const string ATTACK_CHARGE_SPEED = BODY_GENERAL + "Attack_Charge_Speed";
		public const string ATTACK_PERFORM_SPEED = BODY_GENERAL + "Attack_Perform_Speed";
		public const string MOVEMENT_SPEED = BODY_GENERAL + "Movement_Speed"; // The agent's base movement speed multiplier.

		// Fire
		private const string BODY_FIRE = BODY_STAT + "FIRE/";
		public const string ENERGY = BODY_FIRE + "Energy"; // POINTSTAT: Amount of spendable force-points.
		public const string POWER = BODY_FIRE + "Power"; // PHYSIC: The body's physical output force (increases impact).
		public const string STRENGTH = BODY_FIRE + "Strength"; // The body's lifting strength in KG.

		// Light
		private const string BODY_LIGHT = BODY_STAT + "LIGHT/";
		public const string STATIC = BODY_LIGHT + "Static"; // POINTSTAT: Amount of spendable charging points.
		public const string PRECISION = BODY_LIGHT + "Precision"; // PHYSIC: The body's critical precision (crit quality).
		public const string STORM_SPEED = BODY_LIGHT + "Storm_Speed"; // Speed while storming during a charged attack.

		// Air
		private const string BODY_AIR = BODY_STAT + "AIR/";
		public const string STAMINA = BODY_AIR + "Stamina"; // POINTSTAT: Amount of spendable movement points.
		public const string PLIANCY = BODY_AIR + "Pliancy"; // PHYSIC: The ability to roll with hits (crit glancing / impact padding).
		public const string DASH_SPEED = BODY_AIR + "Dash_Speed"; // Speed of the initial dash burst.
		public const string GLIDE_SPEED = BODY_AIR + "Glide_Speed"; // Speed of the gliding state (after dashing).
		public const string JUMP_SPEED = BODY_AIR + "Jump_Speed"; // Speed multiplier of the jump performance.
		public const string AIR_CONTROL = BODY_AIR + "Air_Control"; // Amount of air-control (0-1).

		// Spirit
		private const string BODY_SPIRIT = BODY_STAT + "SPIRIT/";
		public const string GRACE = BODY_SPIRIT + "Grace"; // POINTSTAT: Amount of spendable damage negation points.
		public const string PROTECTION = BODY_SPIRIT + "Protection"; // PHYSIC: Defends against all non-physical damage. (magic / status resistance)

		// Water
		private const string BODY_WATER = BODY_STAT + "WATER/";
		public const string MANA = BODY_WATER + "Mana"; // POINTSTAT: Amount of spendable magic points.
		public const string POTENCY = BODY_WATER + "Potency"; // PHYSIC: The body's magic power output (increases non-physical damage).
		public const string PROFICIENCY = BODY_WATER + "Proficiency"; // The mind's magic proficiency vs spell complexity.

		// Nature
		private const string BODY_NATURE = BODY_STAT + "NATURE/";
		public const string HEALTH = BODY_NATURE + "Health"; // POINTSTAT: Amount of life points away from death.
		public const string PRESERVATION = BODY_NATURE + "Preservation"; // PHYSIC: The body's preservation points (lowers frailty & recovery delay)
		public const string RECOVERY = BODY_NATURE + "Recovery"; // Overall PointStat recovery multiplier.
		public const string RECOVERY_DELAY = BODY_NATURE + "Recovery_Delay"; // Overall PointStat recovery delay multiplier.
		public const string FRAILTY = BODY_NATURE + "Frailty"; // Overall PointStat frailty multiplier, defines vulnerability of reserves.

		// Earth
		private const string BODY_EARTH = BODY_STAT + "EARTH/";
		public const string ENDURANCE = BODY_EARTH + "Endurance"; // POINTSTAT: Amount absorbable force points before being stunned.
		public const string PROOFING = BODY_EARTH + "Proofing"; // PHYSIC: The body's resistance to piercing (sharp defence).
		public const string POISE = BODY_EARTH + "Poise"; // Endurance cost divider.
		public const string GUARD = BODY_EARTH + "Guard"; // Total amount of active guarding defence.

		// Void
		private const string BODY_VOID = BODY_STAT + "VOID/";
		public const string MALICE = BODY_VOID + "Malice"; // POINTSTAT: Amount of available damage bonus points.
		public const string PIERCING = BODY_VOID + "Piercing"; // PHYSIC: The body's sharp damage output.

		#endregion Bodily Stats

		// SOUL STATS
		#region Soul Stats
		private const string SOUL_STAT = IStatIdentifiers.STATS + "SOUL/";

		// GENERAL
		private const string SOUL_GENERAL = SOUL_STAT + "GENERAL/";
		public const string AETHER = SOUL_GENERAL + "Aether"; // Currency of the soul, equal to GOLD and EXP.
		public const string VIRTUE = SOUL_GENERAL + "Virtue"; // Total amount of virtuous aether gained.
		public const string SIN = SOUL_GENERAL + "Sin"; // Total amount of sinful aether gained.
		public const string ALIGNMENT = SOUL_GENERAL + "Alignment"; // Virtue/Sin ratio.

		// Fire
		private const string SOUL_FIRE = SOUL_STAT + "FIRE/";

		// Light
		private const string SOUL_LIGHT = SOUL_STAT + "LIGHT/";

		// Air
		private const string SOUL_AIR = SOUL_STAT + "AIR/";

		// Spirit
		private const string SOUL_SPIRIT = SOUL_STAT + "SPIRIT/";
		public const string LUCK = SOUL_SPIRIT + "Luck"; // Stat influencing all randomness relating to the agent.

		// Water
		private const string SOUL_WATER = SOUL_STAT + "WATER/";

		// Nature
		private const string SOUL_NATURE = SOUL_STAT + "NATURE/";

		// Earth
		private const string SOUL_EARTH = SOUL_STAT + "EARTH/";

		// Void
		private const string SOUL_VOID = SOUL_STAT + "VOID/";

		#endregion Soul Stats

		#region Sub Stats

		/// <summary>
		/// <see cref="StringExtensions.SubStat(string, string)"/>.
		/// </summary>
		public const string SUB_STAT = IStatIdentifiers.STATS + "SUB/";

		// Point-stats
		public const string SUB_MAX = SUB_STAT + "Max"; // Maximum amount of points.
		public const string SUB_RESERVE = SUB_STAT + "Reserve"; // Recoverable amount of points.
		public const string SUB_FRAILTY = SUB_STAT + "Frailty"; // Vulnerability of the reserve.
		public const string SUB_RECOVERY = SUB_STAT + "Recovery"; // Amount recovered per second.
		public const string SUB_RECOVERY_DELAY = SUB_STAT + "Recovery_Delay"; // Time until recovery starts.
		public const string SUB_DRAIN = SUB_STAT + "Drain"; // Point cost multiplier.
		public const string SUB_GAIN = SUB_STAT + "Gain"; // Point gain multiplier.

		// Limbs
		public const string SUB_RIGHT_HAND = SUB_STAT + "Right Hand";
		public const string SUB_LEFT_HAND = SUB_STAT + "Left Hand";

		#endregion Sub Stats
	}
}
