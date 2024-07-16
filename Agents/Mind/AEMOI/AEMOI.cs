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
		public event Action<float> OnMindUpdateEvent;

		/// <inheritdoc/>
		public event Action OnMindUpdatedEvent;

		/// <inheritdoc/>
		public bool Active { get; private set; }

		/// <inheritdoc/>
		public Vector8 Personality => personality.Vector8;

		/// <inheritdoc/>
		public (Vector8 stimuli, IEntity source) Motivation { get; private set; }

		private AEMOISettings settings;
		private IOcton personality;
		private List<IAEMOIBehaviour> behaviours;

		private IAEMOIBehaviour activeBehaviour;

		private Dictionary<IEntity, Vector8> stimuli = new Dictionary<IEntity, Vector8>();

		public AEMOI(AEMOISettings settings, IOcton personality, IEnumerable<IAEMOIBehaviour> behaviours = null)
		{
			this.settings = settings;
			this.personality = personality;
			this.behaviours = behaviours == null ? new List<IAEMOIBehaviour>() : new List<IAEMOIBehaviour>(behaviours);
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

			Active = false;
		}

		/// <inheritdoc/>
		public void Update(float delta)
		{
			// Gather senses.
			OnMindUpdateEvent?.Invoke(delta);

			// Simulate stimuli according to Personality and dampen them to limit buildup.
			foreach (KeyValuePair<IEntity, Vector8> kvp in stimuli)
			{
				stimuli[kvp.Key] = kvp.Value.Simulate(Vector8.Zero, Personality, settings.EmotionSimulation * delta);
				stimuli[kvp.Key] = kvp.Value.Lerp(Vector8.Zero, settings.EmotionDamping * delta);
			}

			// Set the current highest motivation.
			Motivation = GetMotivation();

			// Check whether the current behaviour can/needs to be switched.
			ReassessBehaviour();

			// Mind has been updated.
			OnMindUpdatedEvent?.Invoke();
		}

		#endregion Activity

		#region Stimulation

		/// <inheritdoc/>
		public void Stimulate(Vector8 stimulation, IEntity source)
		{
			stimuli[source] = (stimuli[source] + stimulation * Personality).Clamp(0f, 100f); // TODO: 100 is a magic number, requires testing to see actual useful value bounds.
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
		public void AddBehaviour(IAEMOIBehaviour behaviour)
		{
			if (!behaviours.Contains(behaviour))
			{
				behaviours.Add(behaviour);
			}
		}

		/// <inheritdoc/>
		public void AddBehaviours(IEnumerable<IAEMOIBehaviour> behaviours)
		{
			foreach (IAEMOIBehaviour behaviour in behaviours)
			{
				AddBehaviour(behaviour);
			}
		}

		/// <inheritdoc/>
		public void RemoveBehaviour(IAEMOIBehaviour behaviour)
		{
			if (behaviours.Contains(behaviour))
			{
				behaviours.Remove(behaviour);
			}
		}

		/// <inheritdoc/>
		public void RemoveBehaviours(IEnumerable<IAEMOIBehaviour> behaviours)
		{
			foreach (IAEMOIBehaviour behaviour in behaviours)
			{
				RemoveBehaviour(behaviour);
			}
		}
		#endregion Behaviour Management

		private void ReassessBehaviour()
		{
			// If the current behaviour isn't interruptable at the moment, don't even bother.
			if (!activeBehaviour.Interuptable)
			{
				return;
			}

			IAEMOIBehaviour closest = null;
			float closestDistance = float.MaxValue;
			foreach (IAEMOIBehaviour behaviour in behaviours)
			{
				if (behaviour.Valid(Motivation.stimuli, Motivation.source, out float distance) && distance < closestDistance)
				{
					closest = behaviour;
					closestDistance = distance;
				}
			}

			if (closest == null)
			{
				return;
			}

			if (closest == activeBehaviour)
			{
				if (Motivation.source != activeBehaviour.Target)
				{
					// The closest behaviour is already active but the target doesn't match, re-initialize it.
					activeBehaviour.Initialize(Motivation.source);
				}
				return;
			}

			StopBehaviour();
			StartBehaviour(closest);
		}

		private void StopBehaviour()
		{
			if (activeBehaviour == null)
			{
				// There's no active behaviour to stop.
				return;
			}

			activeBehaviour.Stop();
		}

		private void StartBehaviour(IAEMOIBehaviour behaviour)
		{
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
				float max = kvp.Value.GetMax(out int i);
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
