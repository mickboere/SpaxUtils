using System.Collections;
using System.Collections.Generic;
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
		public AllySense AllySense { get; private set; }

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
		private AllySensesSettings allySensesSettings;
		private ProjectileService projectileService;
		private AgentCombatComponent combatComponent;
		private FocusHandler focusHandler;

		private EntityStat aggroStat;
		private bool inCombat;
		private ITargetable lastTarget;

		public void InjectDependencies(IVisionComponent vision,
			IHittable hittable, [Optional] ISpawnpoint spawnpoint,
			AgentStatHandler statHandler, CombatSensesSettings settings,
			AllySensesSettings allySensesSettings,
			ProjectileService projectileService, AgentCombatComponent combatComponent,
			[Optional] FocusHandler focusHandler)
		{
			this.vision = vision;
			this.hittable = hittable;
			this.spawnpoint = spawnpoint;
			this.statHandler = statHandler;
			this.settings = settings;
			this.allySensesSettings = allySensesSettings;
			this.projectileService = projectileService;
			this.combatComponent = combatComponent;
			this.focusHandler = focusHandler;

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
			AllySense = new AllySense(Agent, vision, allySensesSettings);

			if (focusHandler != null)
			{
				focusHandler.Register(FocusHandler.PRIORITY_COMBAT, GetCombatFocusPoint);
			}
		}

		public void OnMindDeactivated()
		{
			Agent.Mind.UpdatingEvent -= OnMindUpdating;
			Agent.Mind.MotivatedEvent -= OnMindMotivated;

			hittable.Unsubscribe(this);

			EnemySense.Dispose();
			ProjectileSense.Dispose();
			AllySense.Dispose();

			if (focusHandler != null)
			{
				focusHandler.Unregister(GetCombatFocusPoint);
			}
		}

		private Vector3? GetCombatFocusPoint()
		{
			if (Agent.Targeter.Target == null)
			{
				return null;
			}

			return Agent.Targeter.Target.Point;
		}

		private void OnMindMotivated()
		{
			// Invoked after behaviour reassessment and Balance computation — ActiveTarget is up-to-date.
			IEntity activeTarget = Agent.Mind.ActiveTarget;
			if (activeTarget != null)
			{
				ITargetable target = activeTarget.GetEntityComponent<ITargetable>();
				if (Agent.Targeter.Target == null || Agent.Targeter.Target != target)
				{
					Agent.Targeter.SetTarget(target);
					lastTarget = target;
				}
			}
			else if (lastTarget != null && Agent.Targeter.Target == lastTarget)
			{
				Agent.Targeter.SetTarget(null);
				lastTarget = null;
			}

			// Aggro = unsigned magnitude sum across all tracked entity stimuli.
			float rawAggro = 0f;
			foreach (KeyValuePair<IEntity, Vector8> kv in Agent.Mind.Stimuli)
			{
				rawAggro += kv.Value.AbsSum();
			}
			aggroStat.BaseValue = rawAggro;

			if (!inCombat && aggroStat >= aggroEnterThreshold)
			{
				inCombat = true;
				Agent.Brain.TryTransition(AgentStateIdentifiers.COMBAT);
			}
			else if (inCombat && aggroStat <= aggroExitThreshold)
			{
				inCombat = false;
				Agent.Brain.TryTransition(AgentStateIdentifiers.PASSIVE);
			}
		}

		private void OnMindUpdating(float delta)
		{
			// Invoked when the mind starts its current update cycle.
			EnemySense.Sense(delta);
			ProjectileSense.Sense(delta);
			AllySense.Sense(delta);
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

			// Foe-directed emotions are negative; flip sign before sending.
			Agent.Mind.Stimulate(-stim, hitData.Hitter);
		}
	}
}
