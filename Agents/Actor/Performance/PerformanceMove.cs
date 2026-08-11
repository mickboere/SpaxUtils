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
		#region Tooltips

		private const string TT_CHARGE_DURATION = "How long a full charge takes, in seconds. Charging itself can continue past it, until the charge stat drains.";
		private const string TT_MIN_CHARGE = "Minimum charge required before performing, as a fraction of the charge duration.";
		private const string TT_REQUIRE_MIN_CHARGE = "TRUE: Releasing input before completing charge will cancel.\nFALSE: Releasing input before completing charge will continue and automatically perform.";
		private const string TT_MIN_DURATION = "Minimum performing duration of this move.";
		private const string TT_CHARGE_FADEOUT = "Duration of transition from charge pose to performing pose, relative to MinDuration.";
		private const string TT_RELEASE = "Interuptable sustain / fadeout time after a successful performance.";

		#endregion Tooltips

		#region Properties

		public string Name => string.IsNullOrWhiteSpace(name) ? base.name : name;
		public string Description => description;

		public PerformanceAnimationType AnimationType => animationType;
		public int AnimationIndex => animationIndex;
		public PosingData PosingData => posingData;
		public AnimationTimeline Timeline => timeline;
		public IReadOnlyList<BehaviourAsset> Behaviour => behaviour;
		public IReadOnlyList<MoveFollowUp> FollowUps => followUps;

		// A phase region's PRESENCE is the flag; the inspector checkbox only adds or removes it.
		public bool HasCharge => UseTimeline
			? timeline.TryGetMarker(TimelineMarkerIdentifiers.CHARGING, out _)
			: hasCharge;

		public float ChargeDuration => UseTimeline ? TimelineChargeDuration() : chargeDuration;

		/// <summary>Seconds, from a 0-1 fraction of <see cref="ChargeDuration"/> - the charge cannot outlast itself.</summary>
		public float MinCharge => minCharge * ChargeDuration;

		public bool RequireMinCharge => requireMinCharge;
		public string ChargeSpeedMultiplierStat => chargeSpeedMultiplier;
		public StatCost ChargeCost => chargeCost;

		public bool HasPerformance => UseTimeline
			? timeline.TryGetMarker(TimelineMarkerIdentifiers.PERFORMING, out _)
			: hasPerformance;
		public float MinDuration => HasPerformance ? (UseTimeline ? TimelineMinDuration() : minDuration) : 0f;
		public float ChargeFadeout => chargeFadeout;
		public float Release => UseTimeline ? TimelineRelease() : release;
		public float TotalDuration => MinDuration + Release;
		public string PerformSpeedMultiplierStat => performSpeedMultiplier;
		public StatCost PerformCost => performCost;

		public float CancelDuration => cancelDuration;

		// MIGRATION LAYER, methods further down. Every timing member answers "timeline if authored, serialized
		// float otherwise", so PoseSequence moves keep working and no consumer ever changes.

		/// <summary>Whether this move's timing comes from timeline markers rather than serialized floats.</summary>
		public bool UseTimeline => animationType == PerformanceAnimationType.Timeline && timeline != null;

		/// <summary>
		/// Marker identifiers meaningful to this kind of move, offered when authoring its timeline.
		/// Chronological, since that is the order they are read in.
		/// </summary>
		public virtual IEnumerable<string> MarkerSuggestions => new[]
		{
			TimelineMarkerIdentifiers.CHARGING,
			TimelineMarkerIdentifiers.PERFORMING,
			TimelineMarkerIdentifiers.FINISHING
		};

		/// <summary>
		/// Clip position at which the swing begins. Markers are absolute clip positions while RunTime is
		/// measured from here, so every conversion goes through it.
		/// </summary>
		protected float PerformOrigin => timeline.TimeOf(TimelineMarkerIdentifiers.PERFORMING, 0f);

		#endregion Properties

		[SerializeField] new private string name;
		[SerializeField, TextArea] private string description;

		[SerializeField] private PerformanceAnimationType animationType;
		[SerializeField] private int animationIndex;
		[SerializeField] private PosingData posingData;
		[SerializeField] private AnimationTimeline timeline;
		[SerializeField, Expandable] private List<BehaviourAsset> behaviour;
		[SerializeField] private List<MoveFollowUp> followUps;
		[SerializeField] private float cancelDuration = 0.25f;
		[SerializeField, Tooltip(TT_RELEASE)] private float release = 0.5f;

		[SerializeField] private bool hasCharge;
		[SerializeField, Tooltip(TT_CHARGE_DURATION)] private float chargeDuration = 1f;
		[SerializeField, Range(0f, 1f), Tooltip(TT_MIN_CHARGE)] private float minCharge = 0.3f;
		[SerializeField, Tooltip(TT_REQUIRE_MIN_CHARGE)] private bool requireMinCharge;
		// No attack-flavoured default: a move states its own pacing stat or runs at 1x. includeEmpty makes "no stat" selectable,
		// forceOption repairs identifiers left stale by a rename instead of silently keeping a dead string.
		[SerializeField, ConstDropdown(typeof(IStatIdentifiers), includeEmpty: true, forceOption: true)] private string chargeSpeedMultiplier;
		[SerializeField] private StatCost chargeCost;

		[SerializeField] private bool hasPerformance;
		[SerializeField, Tooltip(TT_MIN_DURATION)] private float minDuration = 0.4f;
		[SerializeField, Range(0f, 1f), Tooltip(TT_CHARGE_FADEOUT)] private float chargeFadeout = 0.3f;
		[SerializeField, ConstDropdown(typeof(IStatIdentifiers), includeEmpty: true, forceOption: true)] private string performSpeedMultiplier;
		[SerializeField] private StatCost performCost;

		public override string ToString()
		{
			return $"PerformanceMove(\"{name}\", \"{description}\", hasCharge:{HasCharge}, hasPerformance:{HasPerformance})";
		}

		#region Timeline migration

		/// <summary>
		/// An authored charge ANIMATION states its own length. A zero-length region is a held pose: it says
		/// where the pose sits and nothing about how long you hold it, so the field still decides that.
		/// </summary>
		private float TimelineChargeDuration()
		{
			return timeline.TryGetMarker(TimelineMarkerIdentifiers.CHARGING, out ResolvedMarker charging) &&
				charging.Length > 0f ? charging.Length : chargeDuration;
		}

		private float TimelineMinDuration()
		{
			// The committed swing is exactly the Performing region: PERFORMING start to FINISHING start.
			// Without a FINISHING the swing simply runs to the end of the clip.
			float finishing = timeline.TimeOf(TimelineMarkerIdentifiers.FINISHING, timeline.Duration);
			return Mathf.Max(0f, finishing - PerformOrigin);
		}

		private float TimelineRelease()
		{
			if (!timeline.TryGetMarker(TimelineMarkerIdentifiers.FINISHING, out ResolvedMarker finishing))
			{
				return release;
			}

			// An authored end IS the release, zero included. Only a point states no end at all, and that
			// sustains to the end of the clip. Keyed on EndMode, since a zero-length region resolves as a point.
			return finishing.EndMode == MarkerEnd.Point
				? Mathf.Max(0f, timeline.Duration - finishing.Start)
				: finishing.Length;
		}

		#endregion Timeline migration
	}
}
