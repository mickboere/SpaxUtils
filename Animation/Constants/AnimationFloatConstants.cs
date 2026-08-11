namespace SpaxUtils
{
	public class AnimationFloatConstants : ILabeledDataIdentifiers
	{
		private const string POSES = "Poses/";
		public const string CYCLE_OFFSET = ILabeledDataIdentifiers.FLOAT + POSES + "Cycle offset";

		// CHARGE_WEIGHT is gone: the charge's weight curve lives on the CHARGING marker itself now, which is
		// the thing that governs it. Nothing reads a global identifier for it any more.
	}
}
