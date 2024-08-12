using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Configurable asset containing all data required to act out a performance move.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatMove", menuName = "ScriptableObjects/PerformanceMove")]
	public class PerformanceMove : ScriptableObject, IPerformanceMove
	{
		#region Properties

		public string Name => string.IsNullOrWhiteSpace(name) ? base.name : name;
		public string Description => description;

		public PosingData PosingData => posingData;
		public IReadOnlyList<BehaviourAsset> Behaviour => behaviour;
		public IReadOnlyList<MoveFollowUp> FollowUps => followUps;

		public bool HasCharge => hasCharge;
		public float MinCharge => minCharge;
		public float MaxCharge => maxCharge;
		public bool RequireMinCharge => requireMinCharge;
		public string ChargeSpeedMultiplierStat => chargeSpeedMultiplier;
		public StatCost ChargeCost => chargeCost;

		public bool HasPerformance => hasPerformance;
		public float MinDuration => HasPerformance ? minDuration : 0f;
		public float ChargeFadeout => chargeFadeout;
		public float Release => release;
		public float TotalDuration => MinDuration + Release;
		public string PerformSpeedMultiplierStat => performSpeedMultiplier;
		public StatCost PerformCost => performCost;

		public float CancelDuration => cancelDuration;

		#endregion Properties

		#region Tooltips

		private const string TT_MIN_CHARGE = "Minimum required charge in seconds before performing.";
		private const string TT_REQUIRE_MIN_CHARGE = "TRUE: Releasing input before completing charge will cancel.\nFALSE: Releasing input before completing charge will continue and automatically perform.";
		private const string TT_MAX_CHARGE = "Maximum charging extent in seconds.";
		private const string TT_MIN_DURATION = "Minimum performing duration of this move.";
		private const string TT_CHARGE_FADEOUT = "Duration of transition from charge pose to performing pose, relative to MinDuration.";
		private const string TT_RELEASE = "Interuptable sustain / fadeout time after a successful performance.";

		#endregion Tooltips

		[SerializeField] new private string name;
		[SerializeField, TextArea] private string description;

		[Header("DATA")]
		[SerializeField] private PosingData posingData;
		[SerializeField, Expandable] private List<BehaviourAsset> behaviour;
		[SerializeField] private List<MoveFollowUp> followUps;
		[SerializeField] private float cancelDuration = 0.25f;
		[SerializeField, Tooltip(TT_RELEASE)] private float release = 0.5f;

		[Header("CHARGING")]
		[SerializeField] private bool hasCharge;
		[SerializeField, Conditional(nameof(hasCharge), hide: true), Tooltip(TT_MIN_CHARGE)] private float minCharge = 0.3f;
		[SerializeField, Conditional(nameof(hasCharge), hide: true), Tooltip(TT_MAX_CHARGE)] private float maxCharge = 1f;
		[SerializeField, Conditional(nameof(hasCharge), hide: true), Tooltip(TT_REQUIRE_MIN_CHARGE)] private bool requireMinCharge;
		[SerializeField, Conditional(nameof(hasCharge), hide: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string chargeSpeedMultiplier = AgentStatIdentifiers.ATTACK_CHARGE_SPEED;
		[SerializeField] private StatCost chargeCost;

		[Header("PERFORMANCE")]
		[SerializeField] private bool hasPerformance;
		[SerializeField, Conditional(nameof(hasPerformance), hide: true), Tooltip(TT_MIN_DURATION)] private float minDuration = 0.4f;
		[SerializeField, Conditional(nameof(hasPerformance), hide: true), Range(0f, 1f), Tooltip(TT_CHARGE_FADEOUT)] private float chargeFadeout = 0.3f;
		[SerializeField, Conditional(nameof(hasPerformance), hide: true), ConstDropdown(typeof(IStatIdentifierConstants))] private string performSpeedMultiplier = AgentStatIdentifiers.ATTACK_PERFORM_SPEED;
		[SerializeField] private StatCost performCost;

		public override string ToString()
		{
			return $"PerformanceMove(\"{name}\", \"{description}\", hasCharge:{hasCharge}, hasPerformance:{hasPerformance})";
		}
	}
}
