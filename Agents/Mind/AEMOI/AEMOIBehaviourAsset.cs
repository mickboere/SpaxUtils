using SpaxUtils.StateMachines;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract base class implementing <see cref="IMindBehaviour"/> for <see cref="IMind"/> behaviour assets.
	/// </summary>
	public abstract class AEMOIBehaviourAsset : BehaviourAsset, IMindBehaviour
	{
		/// <summary>
		/// Wheel-neighbour coefficient: the fraction of a primary-axis impulse that bleeds onto its 45° wheel
		/// neighbours / triad siblings. Was cos(45°) ≈ 0.707; tuned down to 0.5 for a less aggressive bleed.
		/// Shared across every AEMOI behaviour so neighbour spread reads consistently.
		/// </summary>
		protected const float NEIGHBOUR_BLEED = 0.577f;

		public string Name => name;
		public virtual int Priority => priority;
		public virtual bool Interuptable { get; protected set; } = true;
		public Vector8 Trigger => trigger;

		protected IAgent Agent { get; private set; }
		protected IMind Mind => Agent.Mind;
		protected Vector8 Personality => Mind.Personality;
		protected Vector8 Emotion => Mind.Emotion;
		protected Vector8 EmotionNormalized => Mind.EmotionNormalized;
		protected IEntity Target => Mind.ActiveTarget;
		protected CallbackService CallbackService { get; private set; }
		protected AgentStatHandler StatHandler { get; private set; }
		protected CombatSensesComponent CombatSenses { get; private set; }
		protected AEMOISettings AEMOISettings { get; private set; }
		protected CombatSensesSettings CombatSensesSettings { get; private set; }
		protected PointStatOctad PointStats => StatHandler.PointStats;

		/// <summary>Shared skill-gated back-off distance (world units, ≥0) — the OUT reasons only, no N/S. For behaviours
		/// that already have their own N/S band (Hostile) and just want the winded/outmatched/mercy push-out on top.</summary>
		protected float StandoffBackoff => OutDesire * CombatSensesSettings.StandoffMax;

		/// <summary>Shared signed standoff shift (world units) for behaviours WITHOUT their own N/S band. Positive = back
		/// off, negative = press in. Primal N/S (Balance) dominates; the OUT reasons layer on, but aggression suppresses them.</summary>
		protected float StandoffOffset
		{
			get
			{
				float ns = (Mind.Balance.S - 0.5f) * 2f; // [-1,1] primal in(-)/out(+)
				float o = OutDesire;
				float offset = ns >= 0f ? ns.Max(o) : ns + o * (1f + ns); // aggression suppresses back-off
				return offset * CombatSensesSettings.StandoffMax;
			}
		}

		/// <summary>0-1 strafe t from mobility (Drive.E, carries difficulty), driving BOTH perlin polarization (intensity)
		/// and frequency — easy/steadfast agents strafe gentler and change direction slower.</summary>
		protected float StrafeMobilityT => Mind.Drive.E.Clamp01();

		/// <summary>0-1 repositioning-input scale from mobility (Drive.E, shaped by MovementScaleCurve): a mobile agent
		/// repositions freely, a dull one barely does — so low-difficulty agents hold still and get caught. Scales strafe/
		/// orbit steer, NOT lunges/chases. The curve lets high-E agents plateau at full power; floored so the low end creeps.</summary>
		protected float MovementScale =>
			Mathf.Lerp(CombatSensesSettings.MovementScaleFloor, 1f, CombatSensesSettings.MovementScaleCurve.Evaluate(Mind.Drive.E));

		/// <summary>[0,1] strongest single reason to hold OUT (Max, not summed): W winded/defensive (Drive), SE mercy
		/// (Emotion), and outmatch ((1−Balance.NE)·Drive.NE — bounded & competence-gated, so dumb agents ignore it).
		/// Raw SW stays out: its distance-growing demand has no satisfier and diverges.</summary>
		private float OutDesire =>
			(Mind.Drive.W * (1f - PointStats.W.PercentageRecoverable))
				.Max(Mind.EmotionNormalized.SE)
				.Max((1f - Mind.Balance.NE) * Mind.Drive.NE);

		protected EntityStat EntityTimescale;

		[SerializeField] new private string name;
		[SerializeField] protected int priority;
		[SerializeField, FormerlySerializedAs("motivation")] protected Vector8 trigger;
		[SerializeField, Tooltip("Manual multiplier on this behaviour's final selection strength (applied after trigger/axis-count normalization). Default 1. Raise to make the behaviour win more readily, lower to make it recessive — without changing the trigger values or their activation thresholds.")]
		protected float strengthMultiplier = 1f;

		[SerializeField, Tooltip("Behaviour is only valid when the brain is already in the required state. Ignored when Enforce State is also true.")]
		protected bool requireState;

		[SerializeField, Conditional(nameof(requireState), drawToggle: false, hide: false), Tooltip("When selected, forces the brain into the required state.")]
		protected bool enforceState;

		[SerializeField, Conditional(nameof(requireState), drawToggle: false, hide: false), ConstDropdown(typeof(IStateIdentifiers))]
		protected string brainState;

		[SerializeField] private bool debug;

		public void InjectDependencies(IAgent agent, CallbackService callbackService, AgentStatHandler agentStatHandler, CombatSensesComponent combatSenses, AEMOISettings aemoiSettings, CombatSensesSettings combatSensesSettings)
		{
			Agent = agent;
			CallbackService = callbackService;
			StatHandler = agentStatHandler;
			CombatSenses = combatSenses;
			AEMOISettings = aemoiSettings;
			CombatSensesSettings = combatSensesSettings;
			EntityTimescale = Agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
		}

		public virtual (IEntity target, float strength) Evaluate(IEntity candidate, Vector8 candidateStimuli)
		{
			if (candidate == null)
			{
				return (null, 0f);
			}

			if (!Valid(candidateStimuli, candidate, out float strength))
			{
				return (null, 0f);
			}

			return (candidate, strength);
		}

		public virtual bool Valid(Vector8 stimuli, IEntity target, out float strength)
		{
			strength = 0f;

			// When requireBrainState is set and we are NOT also enforcing, the brain must already be in state.
			if (requireState && !enforceState && !Agent.Brain.IsStateActive(brainState))
			{
				return false;
			}

			// Sign-aware threshold check: each non-zero trigger channel requires the stimuli to
			// match sign AND meet the absolute magnitude.
			for (int i = 0; i < 8; i++)
			{
				if (trigger[i].Approx(0))
				{
					continue;
				}

				if (stimuli[i] * Mathf.Sign(trigger[i]) < Mathf.Abs(trigger[i]))
				{
					return false;
				}
			}

			// Strength: the AVERAGE of (stimuli[i] * trigger[i]) over the triggered channels — the sum divided by the
			// channel COUNT (not by Σ|trigger|). So a higher trigger still means higher strength (magnitude scales it),
			// but a multi-axis trigger is NOT inflated by its axis count: N channels at 1 reads the same as a single
			// channel at 1. Positive when stimuli and trigger agree in sign.
			int triggerCount = 0;
			for (int i = 0; i < 8; i++)
			{
				if (!trigger[i].Approx(0))
				{
					strength += stimuli[i] * trigger[i];
					triggerCount++;
				}
			}
			if (triggerCount > 0)
			{
				strength /= triggerCount;
			}
			strength *= strengthMultiplier;

			// Drive-gate ally-directed behaviours (positive triggers, per the sign convention) by the agent's Drive on the
			// answered axis, so it only peels into support it's actually disposed to — a high-Drive.NW agent intercepts, a
			// support-leaning one rallies/retreats. Selection reads raw motivation otherwise, so disposition can't enter.
			// Foe behaviours (negative triggers) have no positive axis and are left untouched.
			float driveGate = 0f;
			int positiveAxes = 0;
			for (int i = 0; i < 8; i++)
			{
				if (trigger[i] > 0f)
				{
					driveGate += Mind.Drive[i];
					positiveAxes++;
				}
			}
			if (positiveAxes > 0)
			{
				strength *= driveGate / positiveAxes;
			}

			return true;
		}

		public override void Start()
		{
			base.Start();
			Log("Start", index: 2);

			if (requireState && enforceState && !string.IsNullOrEmpty(brainState))
			{
				Agent.Brain.TryTransition(brainState);
			}
		}

		public override void Stop()
		{
			base.Stop();
			Log("Stop", index: 2);
		}

		/// <summary>Spacing tracker: lags <paramref name="held"/> toward <paramref name="target"/> at a rate set by
		/// competence (Drive.NE, carries difficulty). A sharp agent tracks the distance crisply; a dull one lags and
		/// wobbles off it — regardless of the tactical situation. WHERE it wants to stand is the target's job, not the
		/// rate's. Pass a sentinel &lt;0 held to snap on the first call. Framerate-independent.</summary>
		protected float LagDistance(float target, ref float held, float delta)
		{
			if (held < 0f)
			{
				held = target;
				return held;
			}
			float rate = CombatSensesSettings.TrackingRate.Lerp(Mind.Drive.NE);
			held = Mathf.Lerp(held, target, 1f - Mathf.Exp(-rate * delta));
			return held;
		}

		protected void Log(string a, string b = "", Color? color = null, int index = 1)
		{
			if (Agent.Debug && debug)
			{
				SpaxDebug.Log(a, b, color: color, callerIndex: index + 1);
			}
		}
	}
}
