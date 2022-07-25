namespace SpaxUtils
{
	public class AnimationFloatConstants : ILabeledDataIdentifierConstants
	{
		private const string POSES = "Poses/";
		public const string LEFT_FOOT_GROUNDED = ILabeledDataIdentifierConstants.FLOAT + POSES + "Left Foot Grounded";
		public const string RIGHT_FOOT_GROUNDED = ILabeledDataIdentifierConstants.FLOAT + POSES + "Right Foot Grounded";
		public const string STEP_INTERVAL = ILabeledDataIdentifierConstants.FLOAT + POSES + "Step Interval";
		public const string STEP_SIZE = ILabeledDataIdentifierConstants.FLOAT + POSES + "Step Size";
		public const string CYCLE_OFFSET = ILabeledDataIdentifierConstants.FLOAT + POSES + "Cycle offset";
	}
}
