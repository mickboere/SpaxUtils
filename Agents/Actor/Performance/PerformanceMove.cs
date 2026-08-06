using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Configurable asset containing all data required to act out a performance move.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(PerformanceMove), menuName = "Performance/" + nameof(PerformanceMove))]
	public class PerformanceMove : ScriptableObject, IPerformanceMove
	{
		#region Properties

		public string Name => string.IsNullOrWhiteSpace(name) ? base.name : name;
		public string Description => description;

		public PerformanceAnimationType AnimationType => animationType;
		public int AnimationIndex => animationIndex;
		public PosingData PosingData => posingData;
		public AnimationTimeline Timeline => timeline;
		public IReadOnlyList<BehaviourAsset> Behaviour => behaviour;
		public IReadOnlyList<MoveFollowUp> FollowUps => followUps;

		// Charging is a HOLD on the charge pose, so how long you must hold is a duration and stays a field.
		// The CHARGING marker only states WHERE that pose sits on the clip - an orthogonal, positional fact.
		public bool HasCharge => hasCharge;
		public float MinCharge => minCharge;
		public float MaxCharge => maxCharge;
		public bool RequireMinCharge => requireMinCharge;
		public string ChargeSpeedMultiplierStat => chargeSpeedMultiplier;
		public StatCost ChargeCost => chargeCost;

		public bool HasPerformance => UseTimeline ? timeline.TryGetMarker(TimelineMarkerIdentifiers.FINISHING, out _) : hasPerformance;
		public float MinDuration => UseTimeline ? TimelineMinDuration() : (hasPerformance ? minDuration : 0f);
		public float ChargeFadeout => chargeFadeout;
		public float Release => UseTimeline ? TimelineRelease() : release;
		public float TotalDuration => MinDuration + Release;
		public string PerformSpeedMultiplierStat => performSpeedMultiplier;
		public StatCost PerformCost => performCost;

		public float CancelDuration => cancelDuration;

		// MIGRATION LAYER, with its methods further down. Every timing member answers "timeline if authored,
		// serialized float otherwise", so existing PoseSequence moves keep working untouched. Deleting these
		// and the floats is the end state; consumers never change because the property names stay put.

		/// <summary>Whether this move's timing comes from timeline markers rather than serialized floats.</summary>
		public bool UseTimeline => animationType == PerformanceAnimationType.Timeline && timeline != null;

		/// <summary>
		/// Clip position at which the swing begins - the start of the Performing region. Markers are absolute
		/// clip positions while RunTime is measured from here, so every conversion goes through it.
		/// Charging (and any lunge) occupies the clip BEFORE this point.
		/// </summary>
		protected float PerformOrigin => timeline.TimeOf(TimelineMarkerIdentifiers.PERFORMING, 0f);

		#endregion Properties

		#region Tooltips

		private const string TT_MIN_CHARGE = "Minimum required charge in seconds before performing.";
		private const string TT_REQUIRE_MIN_CHARGE = "TRUE: Releasing input before completing charge will cancel.\nFALSE: Releasing input before completing charge will continue and automatically perform.";
		private const string TT_MAX_CHARGE = "Maximum charging extent in seconds (this is only used in determining the charge pose, charging itself can be continued until the charge stat is drained).";
		private const string TT_MIN_DURATION = "Minimum performing duration of this move.";
		private const string TT_CHARGE_FADEOUT = "Duration of transition from charge pose to performing pose, relative to MinDuration.";
		private const string TT_RELEASE = "Interuptable sustain / fadeout time after a successful performance.";

		#endregion Tooltips

		[SerializeField] new private string name;
		[SerializeField, TextArea] private string description;

		[Header("DATA")]
		[SerializeField] private PerformanceAnimationType animationType;
		[SerializeField, Conditional(nameof(animationType), 0)] private int animationIndex;
		[SerializeField, Conditional(nameof(animationType), 1)] private PosingData posingData;
		[SerializeField, Conditional(nameof(animationType), 2)] private AnimationTimeline timeline;
		[SerializeField, Expandable] private List<BehaviourAsset> behaviour;
		[SerializeField] private List<MoveFollowUp> followUps;
		[SerializeField] private float cancelDuration = 0.25f;
		[SerializeField, Tooltip(TT_RELEASE)] private float release = 0.5f;

		[Header("CHARGING")]
		[SerializeField] private bool hasCharge;
		[SerializeField, Conditional(nameof(hasCharge), hide: true), Tooltip(TT_MIN_CHARGE)] private float minCharge = 0.3f;
		[SerializeField, Conditional(nameof(hasCharge), hide: true), Tooltip(TT_MAX_CHARGE)] private float maxCharge = 1f;
		[SerializeField, Conditional(nameof(hasCharge), hide: true), Tooltip(TT_REQUIRE_MIN_CHARGE)] private bool requireMinCharge;
		// No attack-flavoured default: a move states its own pacing stat or runs at 1x. includeEmpty makes "no stat" selectable,
		// forceOption repairs identifiers left stale by a rename instead of silently keeping a dead string.
		[SerializeField, Conditional(nameof(hasCharge), hide: true), ConstDropdown(typeof(IStatIdentifiers), includeEmpty: true, forceOption: true)] private string chargeSpeedMultiplier;
		[SerializeField] private StatCost chargeCost;

		[Header("PERFORMANCE")]
		[SerializeField] private bool hasPerformance;
		[SerializeField, Conditional(nameof(hasPerformance), hide: true), Tooltip(TT_MIN_DURATION)] private float minDuration = 0.4f;
		[SerializeField, Conditional(nameof(hasPerformance), hide: true), Range(0f, 1f), Tooltip(TT_CHARGE_FADEOUT)] private float chargeFadeout = 0.3f;
		[SerializeField, Conditional(nameof(hasPerformance), hide: true), ConstDropdown(typeof(IStatIdentifiers), includeEmpty: true, forceOption: true)] private string performSpeedMultiplier;
		[SerializeField] private StatCost performCost;

		public override string ToString()
		{
			return $"PerformanceMove(\"{name}\", \"{description}\", hasCharge:{hasCharge}, hasPerformance:{hasPerformance})";
		}

		#region Timeline migration

		private float TimelineMinDuration()
		{
			// The committed swing is exactly the Performing region: PERFORMING start to FINISHING start.
			float finishing = timeline.TimeOf(TimelineMarkerIdentifiers.FINISHING, timeline.Duration);
			return Mathf.Max(0f, finishing - PerformOrigin);
		}

		private float TimelineRelease()
		{
			if (!timeline.TryGetMarker(TimelineMarkerIdentifiers.FINISHING, out ResolvedMarker finishing))
			{
				return release;
			}

			// A FINISHING region states its own sustain; a bare point sustains to the end of the clip.
			return finishing.IsRegion ? finishing.Length : Mathf.Max(0f, timeline.Duration - finishing.Start);
		}

		#endregion Timeline migration
	}
}
