namespace SpaxUtils
{
	public class AnimationFloatConstants : ILabeledDataIdentifiers
	{
		private const string POSES = "Poses/";
		public const string LEFT_FOOT_GROUNDED = ILabeledDataIdentifiers.FLOAT + POSES + "Left Foot Grounded";
		public const string RIGHT_FOOT_GROUNDED = ILabeledDataIdentifiers.FLOAT + POSES + "Right Foot Grounded";
		public const string STEP_INTERVAL = ILabeledDataIdentifiers.FLOAT + POSES + "Step Interval";
		public const string STEP_SIZE = ILabeledDataIdentifiers.FLOAT + POSES + "Step Size";
		public const string CYCLE_OFFSET = ILabeledDataIdentifiers.FLOAT + POSES + "Cycle offset";
	}
}
