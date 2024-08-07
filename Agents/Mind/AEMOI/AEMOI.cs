using System;
using System.Collections;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// AEMOI: Artificial Emotional Intelligence.
	/// <see cref="IMind"/> implementation that stores sensory stimuli as <see cref="Vector8"/> per outside-<see cref="IEntity"/>.
	/// From these stimuli the agent's strongest motivation can be infered to then be acted upon.
	/// </summary>
	public class AEMOI : IMind
	{
		/// <inheritdoc/>
		public event Action<float> UpdateEvent;

		/// <inheritdoc/>
		public event Action MotivatedEvent;

		/// <inheritdoc/>
		public event Action UpdatedEvent;

		/// <inheritdoc/>
		public bool Active { get; private set; }

		/// <inheritdoc/>
		public Vector8 Personality => personality.Vector8;

		/// <inheritdoc/>
		public (Vector8 motivation, IEntity target) Motivation { get; private set; }

		private IDependencyManager dependencyManager;
		private AEMOISettings settings;
		private IOcton personality;
		private List<IMindBehaviour> behaviours;

		private IMindBehaviour activeBehaviour;

		private Dictionary<IEntity, Vector8> stimuli = new Dictionary<IEntity, Vector8>();

		public AEMOI(IDependencyManager dependencyManager, AEMOISettings settings, IOcton personality, IEnumerable<IMindBehaviour> behaviours = null)
		{
			this.dependencyManager = dependencyManager;
			this.settings = settings;
			this.personality = personality;
			this.behaviours = behaviours == null ? new List<IMindBehaviour>() : new List<IMindBehaviour>(behaviours);
		}

		#region Activity

		/// <inheritdoc/>
		public void Activate(bool reset = false)
		{
			if (Active)
			{
				return; // Already active.
			}

			if (reset)
			{
				stimuli.Clear();
			}

			Active = true;
		}

		/// <inheritdoc/>
		public void Deactivate()
		{
			if (!Active)
			{
				return; // Already inactive.
			}

			StopBehaviour();

			Active = false;
		}

		/// <inheritdoc/>
		public void Update(float delta)
		{
			// Gather senses.
			UpdateEvent?.Invoke(delta);

			// Simulate stimuli according to Personality and dampen them to limit buildup.
			List<IEntity> sources = new List<IEntity>(stimuli.Keys);
			foreach (IEntity source in sources)
			{
				stimuli[source] = stimuli[source].Simulate(Vector8.Zero, Personality, settings.EmotionSimulation * delta).Lerp(Vector8.Zero, settings.EmotionDamping * delta);
			}

			// Set the current highest motivation.
			Motivation = GetMotivation();
			MotivatedEvent?.Invoke();

			// Check whether the current behaviour can/needs to be switched.
			ReassessBehaviour();

			// Mind has been updated.
			UpdatedEvent?.Invoke();

			//SpaxDebug.Log($"Motivation", Motivation.motivation.ToStringShort());
		}

		#endregion Activity

		#region Stimulation

		/// <inheritdoc/>
		public void Stimulate(Vector8 stimulation, IEntity source)
		{
			if (!stimuli.ContainsKey(source))
			{
				stimuli.Add(source, (stimulation * Personality).Clamp(0f, 100f)); // TODO: 100 is a magic number, requires testing to see actual useful value bounds.
			}
			else
			{
				stimuli[source] = (stimuli[source] + stimulation * Personality).Clamp(0f, 100f);
			}
		}

		/// <inheritdoc/>
		public void Satisfy(Vector8 satisfaction, IEntity source)
		{
			Stimulate(-satisfaction, source);
		}

		#endregion

		#region Behaviour

		#region Behaviour Management
		/// <inheritdoc/>
		public void AddBehaviour(IMindBehaviour behaviour)
		{
			if (!behaviours.Contains(behaviour))
			{
				behaviours.Add(behaviour);
				dependencyManager.Inject(behaviour);
			}
		}

		/// <inheritdoc/>
		public void AddBehaviours(IEnumerable<IMindBehaviour> behaviours)
		{
			foreach (IMindBehaviour behaviour in behaviours)
			{
				AddBehaviour(behaviour);
			}
		}

		/// <inheritdoc/>
		public void RemoveBehaviour(IMindBehaviour behaviour)
		{
			if (behaviours.Contains(behaviour))
			{
				behaviours.Remove(behaviour);
			}
		}

		/// <inheritdoc/>
		public void RemoveBehaviours(IEnumerable<IMindBehaviour> behaviours)
		{
			foreach (IMindBehaviour behaviour in behaviours)
			{
				RemoveBehaviour(behaviour);
			}
		}
		#endregion Behaviour Management

		private void ReassessBehaviour()
		{
			// If the current behaviour isn't interruptable, don't even bother.
			if (activeBehaviour != null && !activeBehaviour.Interuptable)
			{
				return;
			}

			IMindBehaviour match = null;
			float strongest = float.MaxValue;
			foreach (IMindBehaviour behaviour in behaviours)
			{
				if (behaviour.Valid(Motivation.motivation, Motivation.target, out float strength) && // Ensure behaviour is valid.
					(match == null || behaviour.Priority > match.Priority || // If priority exceeds current, set new match.
					(behaviour.Priority == match.Priority && strength > strongest))) // If priority matches current, set match to strongest.
				{
					match = behaviour;
					strongest = strength;
				}
			}

			if (match == null || match == activeBehaviour)
			{
				return;
			}

			StopBehaviour();
			StartBehaviour(match);
		}

		private void StopBehaviour()
		{
			if (activeBehaviour == null)
			{
				// There's no active behaviour to stop.
				return;
			}

			activeBehaviour.Stop();
			activeBehaviour = null;
		}

		private void StartBehaviour(IMindBehaviour behaviour)
		{
			activeBehaviour = behaviour;
			behaviour.Start();
		}

		#endregion Behaviour

		/// <summary>
		/// Returns the strongest motivation.
		/// </summary>
		private (Vector8 stimuli, IEntity source) GetMotivation()
		{
			Vector8 motivation = Vector8.Zero;
			IEntity source = null;
			float highest = 0f;

			foreach (KeyValuePair<IEntity, Vector8> kvp in stimuli)
			{
				float max = kvp.Value.Highest(out int i);
				if (max > highest)
				{
					motivation = kvp.Value;
					highest = max;
					source = kvp.Key;
				}
			}

			return (motivation, source);
		}
	}
}
