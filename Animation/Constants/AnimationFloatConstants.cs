namespace SpaxUtils
{
	public class AnimationFloatConstants : ILabeledDataIdentifiers
	{
		private const string POSES = "Poses/";
		public const string CYCLE_OFFSET = ILabeledDataIdentifiers.FLOAT + POSES + "Cycle offset";

		/// <summary>Curve over charge progress deciding how strongly the charge pose asserts itself.</summary>
		public const string CHARGE_WEIGHT = ILabeledDataIdentifiers.FLOAT + POSES + "Charge weight";
	}
}
