using System.Collections;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that tracks all combat variables and sends appropriate stimuli to the mind so the agent can react to them.
	/// Also manages aggro accumulation and drives Passive/Combat brain state transitions.
	/// </summary>
	public class CombatSensesComponent : AgentComponentBase
	{
		public EnemySense EnemySense { get; private set; }
		public ProjectileSense ProjectileSense { get; private set; }

		[Header("Aggro")]
		[SerializeField, Tooltip("Aggro level at which the agent transitions to Combat.")]
		private float aggroEnterThreshold = 2f;

		[SerializeField, Tooltip("Aggro level at which the agent transitions back to Passive. Lower than enter to prevent flickering.")]
		private float aggroExitThreshold = 1f;

		private IVisionComponent vision;
		private IHittable hittable;
		private ISpawnpoint spawnpoint;
		private AgentStatHandler statHandler;
		private CombatSensesSettings settings;
		private ProjectileService projectileService;
		private AgentCombatComponent combatComponent;

		private EntityStat aggroStat;
		private bool inCombat;

		public void InjectDependencies(IVisionComponent vision,
			IHittable hittable, [Optional] ISpawnpoint spawnpoint,
			AgentStatHandler statHandler, CombatSensesSettings settings, ProjectileService projectileService, AgentCombatComponent combatComponent)
		{
			this.vision = vision;
			this.hittable = hittable;
			this.spawnpoint = spawnpoint;
			this.statHandler = statHandler;
			this.settings = settings;
			this.projectileService = projectileService;
			this.combatComponent = combatComponent;

			aggroStat = Agent.Stats.GetStat(AgentStatIdentifiers.AGGRO, true, 0f);
		}

		protected void Awake()
		{
			Agent.Mind.ActivatedEvent += OnMindActivated;
			Agent.Mind.DeactivatedEvent += OnMindDeactivated;

			if (Agent.Mind.Active)
			{
				OnMindActivated();
			}
		}

		public void OnMindActivated()
		{
			Agent.Mind.UpdatingEvent += OnMindUpdating;
			Agent.Mind.MotivatedEvent += OnMindMotivated;

			hittable.Subscribe(this, OnReceivedHitEvent);

			EnemySense = new EnemySense(Agent, vision, statHandler, combatComponent, settings);
			ProjectileSense = new ProjectileSense(Agent, projectileService);
		}

		public void OnMindDeactivated()
		{
			Agent.Mind.UpdatingEvent -= OnMindUpdating;
			Agent.Mind.MotivatedEvent -= OnMindMotivated;

			hittable.Unsubscribe(this);

			EnemySense.Dispose();
			ProjectileSense.Dispose();
		}

		private void OnMindMotivated()
		{
			// Invoked when the mind's motivation has settled.
			if (Agent.Mind.Motivation.target != null)
			{
				if (Agent.Targeter.Target == null || Agent.Targeter.Target.Entity != Agent.Mind.Motivation.target)
				{
					// Set target to the entity responsible for the mind's motivation.
					Agent.Targeter.SetTarget(Agent.Mind.Motivation.target.GetEntityComponent<ITargetable>());
				}
			}
			else
			{
				// There is no motivation target.
				Agent.Targeter.SetTarget(null);
			}

			// Aggro = sum of all threat-relevant emotion axes (all except SE).
			Vector8 emotion = Agent.Mind.Motivation.emotion;
			float rawAggro = emotion.Sum() - emotion.SE;
			aggroStat.BaseValue = rawAggro;
			float effectiveAggro = aggroStat.Value;

			if (!inCombat && effectiveAggro >= aggroEnterThreshold)
			{
				inCombat = true;
				Agent.Brain.TryTransition(AgentStateIdentifiers.COMBAT);
			}
			else if (inCombat && effectiveAggro <= aggroExitThreshold)
			{
				inCombat = false;
				Agent.Brain.TryTransition(AgentStateIdentifiers.PASSIVE);
			}

			//SpaxDebug.Log($"[{Agent.Identification.Name}] rawAggro={rawAggro}, effectiveAggro={effectiveAggro}, inCombat={inCombat}, enterThreshold={aggroEnterThreshold}");
		}

		private void OnMindUpdating(float delta)
		{
			// Invoked when the mind starts its current update cycle.
			EnemySense.Sense(delta);
			ProjectileSense.Sense(delta);
		}

		private void OnReceivedHitEvent(HitData hitData)
		{
			// Actual damage removed from health.
			float damageDealt = hitData.Data.GetValue<float>(HitDataIdentifiers.DAMAGE_TOTAL, 0f);
			if (damageDealt <= 0f)
			{
				return;
			}

			float curHealth = statHandler.PointStats.SW.Current;

			// Impact in [0..1]:
			//  - small when it's a tiny chip off a healthy pool
			//  - large when you're already low or it was a big chunk.
			float impact01;
			{
				float denom = curHealth + damageDealt;
				impact01 = denom > 0f ? Mathf.Clamp01(damageDealt / denom) : 1f;
			}

			if (impact01 <= 0f)
			{
				return;
			}

			// Personality influence (poles).
			Vector8 pers = Agent.Mind.Personality;
			Vector8 dev = (pers - Vector8.Half) * 2f; // [-1..1]

			// Fire vs Water: anger vs fear.
			float fierceness = Mathf.Clamp01(0.5f + 0.5f * dev.N);   // FIERCENESS
			float carefulness = Mathf.Clamp01(0.5f + 0.5f * dev.S);   // CAREFULNESS

			// Earth: how much we respond with guard/poise.
			float steadfastness = Mathf.Clamp01(0.5f + 0.5f * dev.W);  // STEADFASTNESS

			// Void: how much we turn pain into ruthless hatred.
			float ruthlessness = Mathf.Clamp01(0.5f + 0.5f * dev.NW); // RUTHLESSNESS

			// Split impact into "fight" vs "fear" tendency based on N-S balance.
			float fightBias = fierceness / (fierceness + carefulness + 0.001f); // 0..1
			float anger01 = impact01 * fightBias;
			float fear01 = impact01 * (1f - fightBias);

			// We don't want one short exchange to shoot to 10;
			// cap the per-hit impulse to a modest fraction of MAX_STIM.
			const float MAX_HIT_STIM = AEMOI.MAX_STIM * 0.3f; // e.g. 3 when MAX_STIM=10.
			float baseStim = impact01 * MAX_HIT_STIM;

			float angerStim = anger01 * MAX_HIT_STIM;
			float fearStim = fear01 * MAX_HIT_STIM;

			Vector8 stim = Vector8.Zero;

			// N (Fight) - retaliation / rage.
			stim.N = angerStim * (0.7f + 0.3f * fierceness) - fearStim * 0.2f;

			// S (Retreat) - urge to back off.
			stim.S = fearStim * (0.7f + 0.3f * carefulness);

			// E (Evade) - reflexive evasiveness.
			stim.E = fearStim * (0.3f + 0.4f * (1f - fightBias + carefulness));

			// W (Guard) - raising guard / bracing.
			stim.W = fearStim * (0.2f + 0.6f * steadfastness);

			// NW (Hate / aggro) - long-term hostility from pain.
			float hateGain = baseStim * (0.5f + 0.5f * ruthlessness); // 0..MAX_HIT_STIM
			stim.NW = hateGain;

			// Use the same damping as continuous stimuli (handled in AEMOI.Stimulate).
			Agent.Mind.Stimulate(stim, hitData.Hitter);
		}
	}
}
