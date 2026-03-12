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
		public string Name => name;
		public virtual int Priority => priority;
		public virtual bool Interuptable { get; protected set; } = true;

		protected IAgent Agent { get; private set; }
		protected IMind Mind => Agent.Mind;
		protected Vector8 Personality => Mind.Personality;
		protected Vector8 Emotion => Mind.Motivation.emotion;
		protected IEntity Target => Mind.Motivation.target;

		protected EntityStat EntityTimescale;
		protected CallbackService CallbackService { get; private set; }
		protected AgentStatHandler StatHandler { get; private set; }
		protected CombatSensesComponent CombatSenses { get; private set; }
		protected PointStatOctad PointStats => StatHandler.PointStats;

		[SerializeField] new private string name;
		[SerializeField] protected int priority;
		[SerializeField, FormerlySerializedAs("motivation")] protected Vector8 trigger;

		[SerializeField, Tooltip("Behaviour is only valid when the brain is already in the required state. Ignored when Enforce State is also true.")]
		protected bool requireState;

		[SerializeField, Conditional(nameof(requireState), drawToggle: false, hide: false), Tooltip("When selected, forces the brain into the required state.")]
		protected bool enforceState;

		[SerializeField, Conditional(nameof(requireState), drawToggle: false, hide: false), ConstDropdown(typeof(IStateIdentifiers))]
		protected string brainState;

		[SerializeField] private bool debug;

		public void InjectDependencies(IAgent agent, CallbackService callbackService, AgentStatHandler agentStatHandler, CombatSensesComponent combatSenses)
		{
			Agent = agent;
			CallbackService = callbackService;
			StatHandler = agentStatHandler;
			CombatSenses = combatSenses;
			EntityTimescale = Agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
		}

		public virtual bool Valid(Vector8 motivation, IEntity target, out float strength)
		{
			strength = 0f;

			// When requireBrainState is set and we are NOT also enforcing, the brain must already be in state.
			if (requireState && !enforceState && !Agent.Brain.IsStateActive(brainState))
			{
				return false;
			}

			if (motivation >= trigger)
			{
				// Calculate strength by summing all motivation members that exceed the minimum trigger motivation.
				for (int i = 0; i < 8; i++)
				{
					if (!trigger[i].Approx(0))
					{
						strength += motivation[i] * trigger[i];
					}
				}
				return true;
			}

			return false;
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
