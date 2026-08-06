using UnityEditor;

namespace SpaxUtils
{
	/// <summary>
	/// Adds the melee groups to the <see cref="PerformanceMoveEditor"/> lens.
	/// Its two delays become markers, so under a timeline they read back instead of being editable here.
	/// </summary>
	[CustomEditor(typeof(MeleeCombatMove), true)]
	public class MeleeCombatMoveEditor : PerformanceMoveEditor
	{
		private MeleeCombatMove Melee => (MeleeCombatMove)target;

		protected override void BuildAdditionalGroups()
		{
			Group("Hit Detection")
				.Field("hitBoxes")
				.Field("hitDetectionDelay", () => !Move.UseTimeline)
				.Custom(() => Readout("Hit Delay", Melee.HitDetectionDelay), () => Move.UseTimeline);

			Group("Momentum")
				.Field("sweep")
				.Field("lift")
				.Field("thrust")
				.Field("bodyMassFraction")
				.Field("inertiaDelay", () => !Move.UseTimeline)
				.Custom(() => Readout("Inertia Delay", Melee.InertiaDelay), () => Move.UseTimeline)
				.Field("prelongCharge")
				.Field("prolongThreshold");

			Group("Output")
				.Field("limb")
				.Field("useArmament")
				.Field("slash", () => !Melee.UseArmament)
				.Field("power", () => !Melee.UseArmament)
				.Field("pierce", () => !Melee.UseArmament)
				.Field("outputScale")
				.Field("overrideBalance")
				.Field("chargeBalance", () => Melee.OverrideBalance)
				.Field("performBalance", () => Melee.OverrideBalance);
		}
	}
}
