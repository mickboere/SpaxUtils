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

		public IReadOnlyList<BehaviourAsset> Behaviour => behaviour;
		public IReadOnlyList<MoveFollowUp> FollowUps => followUps;

		public bool HasCharge => hasCharge;
		public float MinCharge => minCharge;
		public float MaxCharge => maxCharge;
		public bool RequireMinCharge => requireMinCharge;
		public string ChargeSpeedMultiplierStat => chargeSpeedMultiplier;

		public bool HasPerformance => hasPerformance;
		public float MinDuration => hasPerformance ? minDuration : 0f;
		public float Release => release;
		public float TotalDuration => MinDuration + Release;
		public string PerformSpeedMultiplierStat => performSpeedMultiplier;

		public float CancelDuration => cancelDuration;

		#endregion Properties

		#region Tooltips

		private const string TT_MIN_CHARGE = "Minimum required charge in seconds before performing.";
		private const string TT_REQUIRE_MIN_CHARGE = "TRUE: Releasing input before completing charge will cancel.\nFALSE: Releasing input before completing charge will continue and automatically perform.";
		private const string TT_MAX_CHARGE = "Maximum charging extent in seconds.";
		private const string TT_MIN_DURATION = "Minimum performing duration of this move.";
		private const string TT_CHARGE_FADEOUT = "Duration of transition from charge pose to performing pose, relative to MinDuration.";
		private const string TT_RELEASE = "Interuptable sustain / fadeout time after the minimum duration.";

		#endregion Tooltips

		[SerializeField] new private string name;
		[SerializeField, TextArea] private string description;

		[Header("Data")]
		[SerializeField] private PoseSequence sequence;
		[SerializeField, Expandable] private List<BehaviourAsset> behaviour;
		[SerializeField] private List<MoveFollowUp> followUps;
		[SerializeField] private float cancelDuration = 0.25f;

		[Header("Preparation")]
		[SerializeField] private bool hasCharge;
		[SerializeField, Conditional(nameof(hasCharge), hide: true), Tooltip(TT_MIN_CHARGE)] private float minCharge = 0.3f;
		[SerializeField, Conditional(nameof(hasCharge), hide: true), Tooltip(TT_MAX_CHARGE)] private float maxCharge = 1f;
		[SerializeField, Conditional(nameof(hasCharge), hide: true), Tooltip(TT_REQUIRE_MIN_CHARGE)] private bool requireMinCharge;
		[SerializeField, Conditional(nameof(hasCharge), hide: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string chargeSpeedMultiplier = AgentStatIdentifiers.ATTACK_CHARGE_SPEED;

		[Header("Performance")]
		[SerializeField] private bool hasPerformance;
		[SerializeField, Conditional(nameof(hasPerformance), hide: true), Tooltip(TT_MIN_DURATION)] private float minDuration = 0.4f;
		[SerializeField, Conditional(nameof(hasPerformance), hide: true), Range(0f, 1f), Tooltip(TT_CHARGE_FADEOUT)] private float chargeFadeout = 0.3f;
		[SerializeField, Tooltip(TT_RELEASE)] private float release = 0.5f;
		[SerializeField, Conditional(nameof(hasPerformance), hide: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string performSpeedMultiplier = AgentStatIdentifiers.ATTACK_PERFORM_SPEED;

		/// <inheritdoc/>
		public PoseTransition Evaluate(float chargeTime, float performTime, out float weight, float cancelTime = 0f)
		{
			// Charging.
			IPose chargePose = sequence.Get(0);
			float chargeWeight = chargePose.EvaluateTransition(Mathf.Clamp01(chargeTime / maxCharge));

			// Performing.
			float performanceWeight = hasPerformance ? Mathf.Clamp01(performTime / (MinDuration * chargeFadeout)).InOutSine() : Mathf.Sign(performTime);
			weight = Mathf.Lerp(chargeWeight, 1f - Mathf.Clamp01((performTime - MinDuration) / Release), performanceWeight).Lerp(0f, cancelTime / CancelDuration);

			return sequence.Evaluate(hasPerformance ? performTime : 0f);
		}

		public override string ToString()
		{
			return $"PerformanceMove(\"{name}\", \"{description}\", hasCharge:{hasCharge}, hasPerformance:{hasPerformance})";
		}
	}
}
