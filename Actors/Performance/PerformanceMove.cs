using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Configurable <see cref="IPerformanceMove"/> asset that can be evaluated to return poses.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatMove", menuName = "ScriptableObjects/PerformanceMove")]
	public class PerformanceMove : ScriptableObject, IPerformanceMove
	{
		#region Properties

		public string Name => string.IsNullOrWhiteSpace(name) ? base.name : name;
		public string Description => description;
		public float MinCharge => minCharge;
		public float MaxCharge => maxCharge;
		public bool RequireMinCharge => requireMinCharge;
		public string ChargeSpeedMultiplierStat => chargeSpeedMultiplier;
		public float MinDuration => minDuration;
		public float Release => release;
		public float TotalDuration => MinDuration + Release;
		public string PerformSpeedMultiplierStat => performSpeedMultiplier;

		#endregion // Properties

		#region Tooltips

		private const string TT_MIN_CHARGE = "Minimum required charge in seconds before performing.";
		private const string TT_REQUIRE_MIN_CHARGE = "TRUE: Releasing input before completing charge will cancel.\nFALSE: Releasing input before completing charge will continue and automatically perform.";
		private const string TT_MAX_CHARGE = "Maximum charging extent in seconds.";
		private const string TT_MIN_DURATION = "Minimum performing duration of this move.";
		private const string TT_CHARGE_FADEOUT = "Duration of transition from charge pose to performing pose, relative to MinDuration.";
		private const string TT_RELEASE = "Interuptable sustain / fadeout time after the minimum duration.";

		#endregion

		[SerializeField] new private string name;
		[SerializeField, TextArea] private string description;
		[SerializeField] private PoseSequence sequence;

		[Header("Preparation")]
		[SerializeField, Tooltip(TT_MIN_CHARGE)] private float minCharge = 0.3f;
		[SerializeField, Tooltip(TT_MAX_CHARGE)] private float maxCharge = 1f;
		[SerializeField, Tooltip(TT_REQUIRE_MIN_CHARGE)] private bool requireMinCharge;
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string chargeSpeedMultiplier = AgentStatIdentifiers.ATTACK_CHARGE_SPEED;

		[Header("Performance")]
		[SerializeField, Tooltip(TT_MIN_DURATION)] private float minDuration = 0.4f;
		[SerializeField, Range(0f, 1f), Tooltip(TT_CHARGE_FADEOUT)] private float chargeFadeout = 0.3f;
		[SerializeField, Tooltip(TT_RELEASE)] private float release = 0.5f;
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string performSpeedMultiplier = AgentStatIdentifiers.ATTACK_PERFORM_SPEED;

		/// <inheritdoc/>
		public PoseTransition Evaluate(float chargeTime, float performTime, out float weight)
		{
			// Charging.
			IPose chargePose = sequence.Get(0);
			float chargeWeight = chargePose.EvaluateTransition(Mathf.Clamp01(chargeTime / maxCharge));

			// Performing.
			float performanceWeight = Mathf.Clamp01(performTime / (MinDuration * chargeFadeout)).InOutSine();
			weight = Mathf.Lerp(chargeWeight, 1f - Mathf.Clamp01((performTime - MinDuration) / Release), performanceWeight);

			return sequence.Evaluate(performTime);
		}
	}
}
