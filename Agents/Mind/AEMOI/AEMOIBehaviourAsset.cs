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

		protected EntityStat EntityTimescale;
		protected CallbackService CallbackService { get; private set; }
		protected AgentStatHandler StatHandler { get; private set; }
		protected CombatSensesComponent CombatSenses { get; private set; }
		protected AEMOISettings AEMOISettings { get; private set; }
		protected CombatSensesSettings CombatSensesSettings { get; private set; }
		protected PointStatOctad PointStats => StatHandler.PointStats;

		/// <summary>Extra standoff distance a vulnerable agent keeps from a foe — the shared "back away when I can't
		/// afford to trade" tell used by every strafing/standoff behaviour. Reached by whichever is greater: the
		/// cautious lean of the S↔N axis (Balance.S — zero at/below neutral S 0.5, ramping to full at S=1) OR endurance
		/// depletion (zero at full W, full at empty W). So a worn-down agent of ANY temperament holds recovery distance
		/// — WITHOUT fleeing (that stays fear's job) — turning standoff time into the W it needs to guard again.</summary>
		protected float CautiousSpacingBonus =>
			Mathf.Max(Mind.Balance.S.Remap(0f, 1f, 0.5f, 1f), 1f - PointStats.W.PercentageRecoverable)
			* CombatSensesSettings.CautiousSpacingMax;

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

		protected void Log(string a, string b = "", Color? color = null, int index = 1)
		{
			if (Agent.Debug && debug)
			{
				SpaxDebug.Log(a, b, color: color, callerIndex: index + 1);
			}
		}
	}
}
