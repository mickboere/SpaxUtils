namespace SpaxUtils
{
	/// <summary>
	/// All EXP reward sources; the deeds an agent can be rewarded experience for.
	/// Weights and anti-farm decay per source are configured in <see cref="ExpSettings"/>.
	/// </summary>
	public class ExpSources : IExpSources
	{
		// Fire
		private const string FIRE = "FIRE/";
		public const string POWER_OUTPUT = FIRE + "Power Output"; // Blunt damage dealt.
		public const string ENERGY_OVERDRAW = FIRE + "Energy Overdraw"; // Energy spent beyond empty.

		// Light
		private const string LIGHT = "LIGHT/";
		public const string PIERCE_OUTPUT = LIGHT + "Pierce Output"; // Crit damage dealt.
		public const string STATIC_HIT = LIGHT + "Static Hit"; // Landing a Static-charged attack.
		public const string DEFLECT = LIGHT + "Deflect"; // Deflecting an incoming attack.

		// Air
		private const string AIR = "AIR/";
		public const string JUMP = AIR + "Jump";
		public const string DASH = AIR + "Dash";
		public const string SPRINT = AIR + "Sprint";

		// Spirit
		private const string SPIRIT = "SPIRIT/";
		public const string GRACE_GAIN = SPIRIT + "Grace Gain";
		public const string GRACE_DRAIN = SPIRIT + "Grace Drain"; // Grace absorbing mortal damage.
		public const string NPC_HELP = SPIRIT + "NPC Help"; // Reserved; awaits the virtue/quest system.

		// Water
		private const string WATER = "WATER/";
		public const string MANA_SPEND = WATER + "Mana Spend";
		public const string MAGIC_OUTPUT = WATER + "Magic Output"; // Reserved; awaits a magic damage channel.

		// Nature
		private const string NATURE = "NATURE/";
		public const string HEALTH_RECOVERY = NATURE + "Health Recovery";
		public const string RESERVE_RECOVERY = NATURE + "Reserve Recovery";

		// Earth
		private const string EARTH = "EARTH/";
		public const string GUARDED_DAMAGE = EARTH + "Guarded Damage"; // Damage cancelled by guard.

		// Void
		private const string VOID = "VOID/";
		public const string SLASH_OUTPUT = VOID + "Slash Output"; // Slashing damage dealt.
		public const string MALICE_KILL = VOID + "Malice Kill"; // Malice spent on a killing blow.
	}
}
