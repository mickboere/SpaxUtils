using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract base class for CORE <see cref="IPerformanceMove.Behaviour"/> assets, meaning assets that fully control the behaviour of a perfomance move.
	/// Implements <see cref="IConditional"/> with configurable grounding and sliding requirements.
	/// Subclasses can override <see cref="IsMet"/> to add additional checks (call base first).
	/// </summary>
	public abstract class CorePerformanceMoveBehaviourAsset : BasePerformanceMoveBehaviourAsset, IUpdatable, IConditional
	{
		protected const string PARAM_MOVE_INDEX = "MoveIndex";
		protected const string PARAM_PREPARE = "Prepare";
		protected const string PARAM_PREPARE_TIME = "PrepareTime";
		protected const string PARAM_PERFORM = "Perform";
		protected const string PARAM_PERFORM_TIME = "PerformTime";

		protected RigidbodyWrapper RigidbodyWrapper { get; private set; }
		protected AgentArmsComponent Arms { get; private set; }
		protected AnimatorPoser Poser { get; private set; }
		protected AnimatorWrapper AnimatorWrapper { get; private set; }
		protected GrounderComponent Grounder { get; private set; }
		protected TimelineGraph TimelineGraph { get; private set; }
		protected IPoserInstructions PoserInstructions { get; private set; }
		protected TimelinePlayer TimelinePlayer { get; private set; }
		protected float Weight { get; private set; }

		[Header("Prerequisites")]
		[SerializeField, Tooltip("Requires the agent to be grounded to perform this move.")]
		private bool requireGrounded = true;
		[SerializeField, Conditional(nameof(requireGrounded)), Tooltip("Whether this move can be performed while sliding. Only relevant when requireGrounded is true.")]
		private bool allowSliding = false;
		[SerializeField, Conditional(nameof(requireGrounded)), Tooltip("Seconds airborne tolerated before a CHARGING performance is cancelled. Covers walking off a ledge as the move starts, so a jump begun at the edge still gets through its minimum charge instead of being cancelled out from under itself.")]
		private float groundLossGrace = 0.2f;

		/// <summary>Whether sliding is tolerated; gates entry via <see cref="IsMet"/> and lets behaviours enforce it mid-performance.</summary>
		protected bool AllowSliding => allowSliding;

		/// <summary>Whether ground is required; gates entry via <see cref="IsMet"/> and lets behaviours enforce it mid-performance.</summary>
		protected bool RequireGrounded => requireGrounded;

		/// <summary>Seconds airborne tolerated before a charge is dropped; shared so behaviours holding a performance can use the same window.</summary>
		protected float GroundLossGrace => groundLossGrace;

		[Header("Control")]
		[SerializeField] private float controlWeightSmoothing = 6f;
		[SerializeField] private bool blockArms;

		private FloatOperationModifier controlMod;
		private float ungroundedTime;

		public virtual bool IsMet(IDependencyManager dependencies)
		{
			if (requireGrounded)
			{
				if (!dependencies.TryGet(out GrounderComponent grounder))
				{
					return false;
				}

				// Deliberately the lenient check: being within the grounder's cast reach is enough to START a move, which
				// is what lets a jump be charged on the way down and chained on landing. Moves that must not RESOLVE
				// mid-air gate their own execution on Standing instead.
				if (!grounder.Grounded)
				{
					return false;
				}

				if (!allowSliding && grounder.Sliding)
				{
					return false;
				}
			}

			return true;
		}

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, AnimatorWrapper animatorWrapper,
			[Optional] AgentArmsComponent arms, [Optional] AnimatorPoser poser, [Optional] GrounderComponent grounder,
			[Optional] TimelineGraph timelineGraph)
		{
			RigidbodyWrapper = rigidbodyWrapper;
			Arms = arms;
			Poser = poser;
			AnimatorWrapper = animatorWrapper;
			Grounder = grounder;
			TimelineGraph = timelineGraph;
		}

		public override void Start()
		{
			base.Start();
			ungroundedTime = 0f;
			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			RigidbodyWrapper.Control.AddModifier(this, controlMod);
			if (Arms != null && blockArms)
			{
				Arms.Weight.AddModifier(this, controlMod);
			}

			// Claim a slot for the whole performance; the playhead is driven from the clock below.
			if (Move.AnimationType == PerformanceAnimationType.Timeline && TimelineGraph != null && Move.Timeline != null)
			{
				TimelinePlayer = TimelineGraph.Play(Move.Timeline, 0f);
			}
		}

		public override void Stop()
		{
			base.Stop();
			RigidbodyWrapper.Control.RemoveModifier(this);
			if (Arms != null && blockArms)
			{
				Arms.Weight.RemoveModifier(this);
			}
			if (Poser != null)
			{
				Poser.RevokeInstructions(this);
			}

			if (TimelinePlayer != null)
			{
				TimelineGraph.Stop(TimelinePlayer);
				TimelinePlayer = null;
			}
		}

		public virtual void ExternalUpdate(float delta)
		{
			// Grounding is a condition for the CHARGE, not the whole performance: once performing, a move is committed —
			// and for the jump, leaving the ground IS the point. Never early-out here; the pose and control weight below
			// still have to run so a cancel animates out instead of freezing.
			// Asymmetric with IsMet on purpose: entry needs Standing, but only genuinely leaving the ground cancels — a
			// dip in GroundedAmount over a bump shouldn't kill a charge that legitimately started. The grace window then
			// covers walking off a ledge as the move begins, which would otherwise cancel it inside its own min-charge.
			if (requireGrounded && Grounder != null && State == PerformanceState.Preparing && !Performer.Canceled)
			{
				ungroundedTime = Grounder.Grounded ? 0f : ungroundedTime + delta;
				if (ungroundedTime > groundLossGrace)
				{
					Performer.TryCancel(true);
				}
			}

			switch (Move.AnimationType)
			{
				case PerformanceAnimationType.Animator:
					Weight = Performer.Weight;
					HandleAnimation();
					break;
				case PerformanceAnimationType.Poser:
					PoserInstructions = Evaluate(out float weight);
					Weight = weight;
					Poser?.ProvideInstructions(this, PoserLayerConstants.BODY, PoserInstructions, 10, Weight);
					break;
				case PerformanceAnimationType.Timeline:
					Weight = Performer.Weight;
					HandleTimeline();
					break;
			}
			// Set control from pose weight.
			float control = 1f - Weight;
			controlMod.SetValue(controlMod.Value < control ? Mathf.Lerp(controlMod.Value, control, controlWeightSmoothing * delta) : control);
		}

		protected virtual void HandleAnimation()
		{
			if (State is PerformanceState.Preparing)
			{
				AnimatorWrapper.SetInteger(PARAM_MOVE_INDEX, Move.AnimationIndex);
			}
			AnimatorWrapper.SetBool(PARAM_PREPARE, State == PerformanceState.Preparing);
			AnimatorWrapper.SetBool(PARAM_PERFORM, State == PerformanceState.Performing);
			float prepareTime = Move.MinCharge > 0f ? Performer.ChargeTime / Move.MinCharge : 0f;
			AnimatorWrapper.SetFloat(PARAM_PREPARE_TIME, prepareTime);
			float performTime = Move.MinDuration > 0f ? Performer.RunTime / Move.MinDuration : 0f;
			AnimatorWrapper.SetFloat(PARAM_PERFORM_TIME, performTime);
		}

		/// <summary>
		/// Drives the clip's playhead from the performance clock. Charging parks on the charge pose; once
		/// performing, RunTime advances forward from it - which is exactly what a hold-then-release reads as.
		/// </summary>
		protected virtual void HandleTimeline()
		{
			if (TimelinePlayer == null)
			{
				return;
			}

			AnimationTimeline timeline = Move.Timeline;
			float charging = timeline.TimeOf(TimelineMarkerIdentifiers.CHARGING, 0f);

			if (State == PerformanceState.Preparing)
			{
				TimelinePlayer.SetTime(ChargePlayhead(timeline, charging));
				Weight = ChargeWeight(timeline, Weight);
			}
			else if (!Move.HasPerformance)
			{
				// No Performing region means there is nothing to play out, so the pose holds where the charge
				// left it and RunTime only fades it back out. Advancing here would scrub the rest of the clip.
				TimelinePlayer.SetTime(
					timeline.TryGetMarker(TimelineMarkerIdentifiers.CHARGING, out ResolvedMarker held)
						? held.End
						: charging);
			}
			else if (this is ILungeProvider lunge && lunge.Lunging &&
				timeline.TryGetMarker(TimelineMarkerIdentifiers.LUNGING, out ResolvedMarker lunging))
			{
				// Driven by gap closure rather than any clock, so near and far lunges both arrive at the
				// end of the region as the swing releases. Without the region the playhead simply holds the
				// final charge pose, since RunTime stays at zero for the duration of the approach.
				// The region's own curve shapes that traversal - linear by default, so opting out is free.
				TimelinePlayer.SetTime(Mathf.Lerp(lunging.Start, lunging.End, lunging.Evaluate(lunge.LungeProgress)));
			}
			else
			{
				// RunTime is measured from where the swing starts, which is the Performing region's start.
				TimelinePlayer.SetTime(timeline.TimeOf(TimelineMarkerIdentifiers.PERFORMING, charging) + Performer.RunTime);
			}

			TimelinePlayer.Weight = Weight;
		}

		/// <summary>
		/// Plays an authored charge ANIMATION across its region as the charge builds. A zero-length region has
		/// nothing to play and simply parks on its pose, which is what a held charge always did.
		/// </summary>
		private float ChargePlayhead(AnimationTimeline timeline, float parked)
		{
			return timeline.TryGetMarker(TimelineMarkerIdentifiers.CHARGING, out ResolvedMarker charging) &&
				charging.Length > 0f
				? Mathf.Lerp(charging.Start, charging.End, ChargeProgress())
				: parked;
		}

		/// <summary>
		/// How strongly the charge pose asserts itself over charge progress, taken from the CHARGING marker's
		/// own curve. Falls back to the clock's weight when none is authored - an empty curve means unauthored.
		/// </summary>
		private float ChargeWeight(AnimationTimeline timeline, float fallback)
		{
			if (!timeline.TryGetMarker(TimelineMarkerIdentifiers.CHARGING, out ResolvedMarker charging) ||
				charging.Curve == null || charging.Curve.length == 0)
			{
				return fallback;
			}

			return Mathf.Clamp01(charging.Curve.Evaluate(ChargeProgress()));
		}

		private float ChargeProgress()
		{
			return Move.ChargeDuration > 0f ? Mathf.Clamp01(Performer.ChargeTime / Move.ChargeDuration) : 0f;
		}

		protected abstract IPoserInstructions Evaluate(out float weight);
	}
}
