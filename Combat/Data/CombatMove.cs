using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Configurable <see cref="ICombatMove"/> asset that can be evaluated to return combat poses.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatMove", menuName = "ScriptableObjects/CombatMove")]
	public class CombatMove : ScriptableObject, ICombatMove
	{
		#region Properties

		/// <inheritdoc/>
		public string Name => string.IsNullOrWhiteSpace(name) ? base.name : name;

		/// <inheritdoc/>
		public string Description => description;

		/// <inheritdoc/>
		public bool RequireMinCharge => requireMinCharge;

		/// <inheritdoc/>
		public float MinCharge => minCharge;

		/// <inheritdoc/>
		public float MaxCharge => maxCharge;

		/// <inheritdoc/>
		public float MinDuration => minDuration;

		/// <inheritdoc/>
		public float Release => release;

		/// <inheritdoc/>
		public float TotalDuration => MinDuration + Release;

		/// <inheritdoc/>
		public Impact Impact => impact;

		/// <inheritdoc/>
		public List<string> HitBoxes => hitBoxes;

		/// <inheritdoc/>
		public string ChargeSpeedMultiplierStat => chargeSpeedMultiplier;

		/// <inheritdoc/>
		public string PerformSpeedMultiplierStat => performSpeedMultiplier;

		/// <inheritdoc/>
		public List<ActCombatPair> Combos => combos;

		#endregion // Properties

		#region Tooltips

		private const string TT_MIN_CHARGE = "Minimum required charge in seconds before performing.";
		private const string TT_REQUIRE_MIN_CHARGE = "TRUE: Releasing input before completing charge will cancel.\nFALSE: Releasing input before completing charge will continue and automatically perform.";
		private const string TT_MAX_CHARGE = "Maximum charging extent in seconds.";
		private const string TT_MIN_DURATION = "Minimum performing duration of this move.";
		private const string TT_CHARGE_FADEOUT = "Duration of transition from charge pose to performing pose, relative to MinDuration.";
		private const string TT_RELEASE = "Interuptable sustain / fadeout time after the minimum duration.";
		private const string TT_IMPACT = "Impact applied to user upon performing the move.";

		#endregion

		[SerializeField] new private string name;
		[SerializeField, TextArea] private string description;
		[SerializeField] private PoseSequence sequence;

		[SerializeField, Header("Charging"), Tooltip(TT_MIN_CHARGE)] private float minCharge = 0.3f;
		[SerializeField, Tooltip(TT_MAX_CHARGE)] private float maxCharge = 1f;
		[SerializeField, Tooltip(TT_REQUIRE_MIN_CHARGE)] private bool requireMinCharge;

		[SerializeField, Header("Performing"), Tooltip(TT_MIN_DURATION)] private float minDuration = 0.4f;
		[SerializeField, Range(0f, 1f), Tooltip(TT_CHARGE_FADEOUT)] private float chargeFadeout = 0.3f;
		[SerializeField, Tooltip(TT_RELEASE)] private float release = 0.5f;
		[SerializeField, Tooltip(TT_IMPACT)] private Impact impact;
		[SerializeField, ConstDropdown(typeof(ITransformLookupIdentifiers), showAdress: true)] private List<string> hitBoxes;

		[SerializeField, Header("Stats"), ConstDropdown(typeof(ILabeledDataIdentifierConstants))] private string chargeSpeedMultiplier;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifierConstants))] private string performSpeedMultiplier;

		[SerializeField, Header("Combo's")] private List<ActCombatPair> combos;

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
