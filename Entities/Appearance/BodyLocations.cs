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

		public const string HEAD_OUTER = HEAD + "/Outer";
		public const string FACE = HEAD + "/Face";
	}
}
