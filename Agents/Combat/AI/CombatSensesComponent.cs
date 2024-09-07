using System.Collections;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component that tracks all combat variables and sends appropriate stimuli to the mind so the agent can react to them.
	/// </summary>
	public class CombatSensesComponent : EntityComponentBase
	{
		public EnemySense EnemySense { get; private set; }
		public ProjectileSense ProjectileSense { get; private set; }

		private IAgent agent;
		private IVisionComponent vision;
		private IHittable hittable;
		private ISpawnpoint spawnpoint;
		private AgentStatHandler statHandler;
		private CombatSensesSettings settings;
		private ProjectileService projectileService;

		public void InjectDependencies(IAgent agent, IVisionComponent vision,
			IHittable hittable, [Optional] ISpawnpoint spawnpoint,
			AgentStatHandler statHandler, CombatSensesSettings settings, ProjectileService projectileService)
		{
			this.agent = agent;
			this.vision = vision;
			this.hittable = hittable;
			this.spawnpoint = spawnpoint;
			this.statHandler = statHandler;
			this.settings = settings;
			this.projectileService = projectileService;
		}

		protected void Awake()
		{
			agent.Mind.ActivatedEvent += OnMindActivated;
			agent.Mind.DeactivatedEvent += OnMindDeactivated;

			if (agent.Mind.Active)
			{
				OnMindActivated();
			}
		}

		public void OnMindActivated()
		{
			agent.Mind.UpdatingEvent += OnMindUpdating;
			agent.Mind.MotivatedEvent += OnMindMotivated;

			hittable.Subscribe(this, OnReceivedHitEvent);

			EnemySense = new EnemySense(agent, spawnpoint, vision, statHandler, settings);
			ProjectileSense = new ProjectileSense(agent, projectileService);
		}

		public void OnMindDeactivated()
		{
			agent.Mind.UpdatingEvent -= OnMindUpdating;
			agent.Mind.MotivatedEvent -= OnMindMotivated;

			hittable.Unsubscribe(this);

			EnemySense.Dispose();
			ProjectileSense.Dispose();
		}

		private void OnMindMotivated()
		{
			// Invoked when the mind's motivation has settled.
			if (agent.Mind.Motivation.target != null)
			{
				// Set target to the entity responsible for the mind's motivation.
				agent.Targeter.SetTarget(agent.Mind.Motivation.target.GetEntityComponent<ITargetable>());
			}
			else
			{
				agent.Targeter.SetTarget(null);
			}
		}

		private void OnMindUpdating(float delta)
		{
			// Invoked when the mind starts its current update cycle.
			EnemySense.Sense(delta);
			ProjectileSense.Sense(delta);
		}

		private void OnReceivedHitEvent(HitData hitData)
		{
			// Invoked when the agent was hit by another entity.
			// Anger is proportionate to relative incoming force.
			// Fear is proportionate to relative incoming damage.
			// Hate is the sum of both.
			float anger = hitData.Result_Force / agent.Body.RigidbodyWrapper.Mass;
			float fear = hitData.Result_Damage / statHandler.PointStatOcton.SW.Current * 3f;
			Vector8 stim = new Vector8()
			{
				N = anger,
				S = fear,
				NW = anger + fear,
			};
			agent.Mind.Stimulate(stim, hitData.Hitter);
		}
	}
}
