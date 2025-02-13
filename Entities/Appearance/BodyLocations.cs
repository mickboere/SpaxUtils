namespace SpaxUtils
{
	public class BodyLocations : IBodyLocations
	{
		#region BASE

		private const string BODY = "BODY/";

		public const string HEAD = BODY + "Head";
		public const string TORSO = BODY + "Torso";
		public const string LEGS = BODY + "Legs";
		public const string FEET = BODY + "Feet";
		public const string ARM_L = BODY + "Arm_L";
		public const string ARM_R = BODY + "Arm_R";
		public const string HAND_L = BODY + "Hand_L";
		public const string HAND_R = BODY + "Hand_R";

		#endregion BASE

		#region SUB-LOCATIONS

		private const string OUTER = "/Outer";

		// HEAD
		public const string HEAD_OUTER = HEAD + OUTER;
		public const string FACE = HEAD + "/Face";

		// TORSO
		public const string TORSO_OUTER = TORSO + OUTER;

		// LEGS
		public const string LEGS_OUTER = LEGS + OUTER;

		// ARMS
		public const string ARM_L_OUTER = ARM_L + OUTER;
		public const string ARM_R_OUTER = ARM_R + OUTER;
		public const string HAND_L_OUTER = HAND_L + OUTER;
		public const string HAND_R_OUTER = HAND_R + OUTER;

		#endregion SUB-LOCATIONS
	}
}
