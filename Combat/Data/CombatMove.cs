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

		public string Name => string.IsNullOrWhiteSpace(name) ? base.name : name;
		public string Description => description;
		public bool RequireMinCharge => requireMinCharge;
		public float MinCharge => minCharge;
		public float MaxCharge => maxCharge;
		public float MinDuration => minDuration;
		public float Release => release;
		public float TotalDuration => MinDuration + Release;
		public List<string> HitBoxes => hitBoxes;
		public Vector3 Inertia => momentum;
		public float ForceDelay => forceDelay;
		public string ChargeSpeedMultiplierStat => chargeSpeedMultiplier;
		public string PerformSpeedMultiplierStat => performSpeedMultiplier;
		public bool Offensive => offensive;
		public string OffenceStat => offenceStat;
		public IList<StatCost> PerformCost => performCost;
		public List<ActCombatPair> FollowUps => followUps;

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

		[SerializeField, Header("Charging"), Tooltip(TT_MIN_CHARGE)] private float minCharge = 0.3f;
		[SerializeField, Tooltip(TT_MAX_CHARGE)] private float maxCharge = 1f;
		[SerializeField, Tooltip(TT_REQUIRE_MIN_CHARGE)] private bool requireMinCharge;

		[SerializeField, Header("Performance"), Tooltip(TT_MIN_DURATION)] private float minDuration = 0.4f;
		[SerializeField, Range(0f, 1f), Tooltip(TT_CHARGE_FADEOUT)] private float chargeFadeout = 0.3f;
		[SerializeField, Tooltip(TT_RELEASE)] private float release = 0.5f;
		[SerializeField, ConstDropdown(typeof(ITransformLookupIdentifiers), showAdress: true)] private List<string> hitBoxes;

		[SerializeField, Header("Forces")] private Vector3 momentum;
		[SerializeField] private float forceDelay;

		[SerializeField, Header("Stats"), ConstDropdown(typeof(IStatIdentifierConstants))] private string chargeSpeedMultiplier = AgentStatIdentifiers.ATTACK_CHARGE_SPEED;
		[SerializeField, ConstDropdown(typeof(IStatIdentifierConstants))] private string performSpeedMultiplier = AgentStatIdentifiers.ATTACK_PERFORM_SPEED;
		[SerializeField, HideInInspector] private bool offensive;
		[SerializeField, Conditional(nameof(offensive), drawToggle: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string offenceStat = AgentStatIdentifiers.OFFENCE;
		[SerializeField] private List<StatCost> performCost;

		[SerializeField, Header("Follow-Ups")] private List<ActCombatPair> followUps;

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
