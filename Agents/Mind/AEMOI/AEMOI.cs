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
		public const float MAX_STIM = 10f;

		/// <inheritdoc/>
		public event Action ActivatedEvent;

		/// <inheritdoc/>
		public event Action DeactivatedEvent;

		/// <inheritdoc/>
		public event Action<float> UpdatingEvent;

		/// <inheritdoc/>
		public event Action MotivatedEvent;

		/// <inheritdoc/>
		public event Action UpdatedEvent;

		/// <inheritdoc/>
		public bool Active { get; private set; }

		/// <inheritdoc/>
		public Vector8 Personality => personality.Vector8;

		/// <inheritdoc/>
		public IReadOnlyDictionary<IEntity, Vector8> Stimuli => stimuli;

		/// <inheritdoc/>
		public (Vector8 motivation, IEntity target) Motivation { get; private set; }

		/// <inheritdoc/>
		public IMindBehaviour ActiveBehaviour { get; private set; }

		private IDependencyManager dependencyManager;
		private AEMOISettings settings;
		private IOcton personality;
		private List<IMindBehaviour> behaviours;

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
			ActivatedEvent?.Invoke();
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
			DeactivatedEvent?.Invoke();
		}

		/// <inheritdoc/>
		public void Update(float delta)
		{
			// Gather senses.
			UpdatingEvent?.Invoke(delta);

			// Disperse and dampen stimuli to limit buildup.
			List<IEntity> sources = new List<IEntity>(stimuli.Keys);
			foreach (IEntity source in sources)
			{
				stimuli[source] = stimuli[source].Disperse(Vector8.Zero, personality.Vector8, settings.EmotionDispersion * delta).Lerp(Vector8.Zero, settings.EmotionDamping * delta);
			}

			// Set the current highest motivation.
			Motivation = GetStrongestStimuli();
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
				stimuli.Add(source, (stimulation * Personality).Clamp(0f, MAX_STIM));
			}
			else
			{
				stimuli[source] = (stimuli[source] + stimulation * Personality).Clamp(0f, MAX_STIM);
			}
		}

		/// <inheritdoc/>
		public void Satisfy(Vector8 satisfaction, IEntity source)
		{
			Stimulate(-satisfaction, source);
		}

		/// <inheritdoc/>
		public Vector8 RetrieveStimuli(IEntity source)
		{
			return Stimuli.ContainsKey(source) ? Stimuli[source] : Vector8.Zero;
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
			if (ActiveBehaviour != null && !ActiveBehaviour.Interuptable)
			{
				return;
			}

			IMindBehaviour match = null;
			float strongest = float.MaxValue;
			foreach (IMindBehaviour behaviour in behaviours)
			{
				if (behaviour.Valid(Motivation.motivation, Motivation.target, out float strength) && // Ensure behaviour is valid.
					(match == null || behaviour.Priority > match.Priority || // If first match or priority exceeds current, set new match.
					(behaviour.Priority == match.Priority && strength > strongest))) // If priority matches current, set match to strongest.
				{
					match = behaviour;
					strongest = strength;
				}
			}

			if (match == null || match == ActiveBehaviour)
			{
				return;
			}

			StopBehaviour();
			StartBehaviour(match);
		}

		private void StopBehaviour()
		{
			if (ActiveBehaviour == null)
			{
				// There's no active behaviour to stop.
				return;
			}

			ActiveBehaviour.Stop();
			ActiveBehaviour = null;
		}

		private void StartBehaviour(IMindBehaviour behaviour)
		{
			ActiveBehaviour = behaviour;
			behaviour.Start();
		}

		#endregion Behaviour

		/// <summary>
		/// Returns the strongest stimuli.
		/// </summary>
		private (Vector8 stimuli, IEntity source) GetStrongestStimuli()
		{
			Vector8 motivation = Vector8.Zero;
			IEntity target = null;
			float highest = 0f;

			foreach (KeyValuePair<IEntity, Vector8> kvp in stimuli)
			{
				float max = kvp.Value.Highest(out int i);
				if (max > highest)
				{
					motivation = kvp.Value;
					highest = max;
					target = kvp.Key;
				}
			}

			return (motivation, target);
		}
	}
}
