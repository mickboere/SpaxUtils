using System.Collections;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that tracks all combat variables and sends appropriate stimuli to the mind so the agent can react to them.
	/// </summary>
	public class CombatSensesComponent : AgentComponentBase
	{
		public EnemySense EnemySense { get; private set; }
		public ProjectileSense ProjectileSense { get; private set; }

		private IVisionComponent vision;
		private IHittable hittable;
		private ISpawnpoint spawnpoint;
		private AgentStatHandler statHandler;
		private CombatSensesSettings settings;
		private ProjectileService projectileService;

		public void InjectDependencies(IVisionComponent vision,
			IHittable hittable, [Optional] ISpawnpoint spawnpoint,
			AgentStatHandler statHandler, CombatSensesSettings settings, ProjectileService projectileService)
		{
			this.vision = vision;
			this.hittable = hittable;
			this.spawnpoint = spawnpoint;
			this.statHandler = statHandler;
			this.settings = settings;
			this.projectileService = projectileService;
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

			EnemySense = new EnemySense(Agent, spawnpoint, vision, statHandler, settings);
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
			//else
			//{
			//	// There is no motivation target.
			//	agent.Targeter.SetTarget(null);
			//}
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
			// Fear is proportionate to relative incoming damage and current health.
			// Hate is the sum of both.
			float anger = 1f;// hitData.Result_Force / agent.Body.RigidbodyWrapper.Mass;
			float fear = hitData.Data.GetValue<float>(HitDataIdentifiers.DAMAGE) / statHandler.PointStats.SW.Current * 3f;
			Vector8 stim = new Vector8()
			{
				N = anger,
				S = fear,
				NW = anger + fear,
			};
			Agent.Mind.Stimulate(stim, hitData.Hitter);
		}
	}
}
