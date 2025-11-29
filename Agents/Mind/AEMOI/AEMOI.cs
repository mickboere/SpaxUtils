using System;
using System.Collections.Generic;
using UnityEngine;

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
		public Vector8 Inclination => inclination.Vector8;

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
		private IOctad inclination;
		private IOctad personality;
		private List<IMindBehaviour> behaviours;

		private Dictionary<IEntity, Vector8> stimuli = new Dictionary<IEntity, Vector8>();
		private Dictionary<IEntity, Vector8> filters = new Dictionary<IEntity, Vector8>();

		public AEMOI(IDependencyManager dependencyManager, AEMOISettings settings, IOctad inclination, IOctad personality, IEnumerable<IMindBehaviour> behaviours = null)
		{
			this.dependencyManager = dependencyManager;
			this.settings = settings;
			this.inclination = inclination;
			this.personality = personality;
			this.behaviours = behaviours == null ? new List<IMindBehaviour>() : new List<IMindBehaviour>(behaviours);
		}

		public void Dispose()
		{
			StopBehaviour();
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

			// Redistribute + damp.
			List<IEntity> sources = new List<IEntity>(stimuli.Keys);
			foreach (IEntity source in sources)
			{
				Vector8 v = stimuli[source];

				// 1) Overflow dispersion based on Inclination + Personality.
				v = RedistributeOverflow(v, delta);

				// 2) Global damping towards zero.
				v = v.FILerp(Vector8.Zero, settings.EmotionDecay * delta);

				// Clamp within range.
				v = v.Clamp(0f, MAX_STIM);

				stimuli[source] = v;
			}

			// Set the current highest motivation.
			Motivation = GetStrongestStimuli();
			MotivatedEvent?.Invoke();

			// Check whether the current behaviour can/needs to be switched.
			ReassessBehaviour();

			// Mind has been updated.
			UpdatedEvent?.Invoke();
		}


		#endregion Activity

		#region Stimulation

		/// <inheritdoc/>
		public void Stimulate(Vector8 stimulation, IEntity source)
		{
			// Current emotional charge towards this source (after previous frames).
			Vector8 current = stimuli.TryGetValue(source, out Vector8 existing) ? existing : Vector8.Zero;

			// Apply per-channel damping so climbing from 0..1 is easy, then slows down towards MAX_STIM.
			stimulation = DampStimulation(stimulation, current);

			if (!stimuli.ContainsKey(source))
			{
				stimuli.Add(source, Filter(stimulation * Inclination).Clamp(0f, MAX_STIM));
			}
			else
			{
				stimuli[source] = (stimuli[source] + Filter(stimulation * Inclination)).Clamp(0f, MAX_STIM);
			}

			Vector8 Filter(Vector8 stim)
			{
				if (filters.ContainsKey(source))
					return stim * filters[source];
				return stim;
			}
		}

		/// <inheritdoc/>
		public void Satisfy(Vector8 satisfaction, IEntity source)
		{
			if (stimuli.ContainsKey(source))
			{
				stimuli[source] = (stimuli[source] - satisfaction).Clamp(0f, MAX_STIM);
			}
		}

		/// <inheritdoc/>
		public Vector8 RetrieveStimuli(IEntity source)
		{
			return Stimuli.ContainsKey(source) ? Stimuli[source] : Vector8.Zero;
		}

		/// <inheritdoc/>
		public void SetFilter(IEntity entity, Vector8 filter)
		{
			filters[entity] = filter;
		}

		/// <inheritdoc/>
		public void RemoveFilter(IEntity entity)
		{
			if (filters.ContainsKey(entity))
			{
				filters.Remove(entity);
			}
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
			// No need to reassess the active behaviour here as that will be done next update loop anyways.
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

			if (match == ActiveBehaviour)
			{
				return;
			}

			StopBehaviour();
			if (match != null)
			{
				StartBehaviour(match);
			}
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
				float max = kvp.Value.Highest(out _);
				//float max = kvp.Value.Sum();
				if (max > highest)
				{
					motivation = kvp.Value;
					highest = max;
					target = kvp.Key;
				}
			}

			return (motivation, target);
		}

		/// <summary>
		/// Clips emotion above OverflowThreshold and redistributes the overflow
		/// using Inclination + Personality as weights. Below the threshold, nothing happens.
		/// </summary>
		private Vector8 RedistributeOverflow(Vector8 v, float delta)
		{
			float threshold = settings.OverflowThreshold;
			float rate = settings.OverflowRedistributionRate;

			// Disabled or degenerate config.
			if (threshold <= 0f || rate <= 0f)
			{
				return v;
			}

			// 1) Clip to threshold and collect overflow.
			Vector8 overflow = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				float level = v[i];
				if (level > threshold)
				{
					float extra = level - threshold;
					float move = Mathf.Min(extra, extra * rate * delta); // do not move more than exists
					v[i] -= move;
					overflow[i] = move;
				}
			}

			float totalOverflow = overflow.Sum();
			if (totalOverflow <= 0f)
			{
				return v; // nothing to redistribute
			}

			// 2) Build dispersion weights from Inclination + Personality.
			//    High inclination + high personality => stronger sink for overflow.
			Vector8 weights = inclination.Vector8 + personality.Vector8;
			float weightSum = weights.Sum();
			if (weightSum <= Mathf.Epsilon)
			{
				return v; // no usable weights, just leave v as-is
			}

			weights = weights / weightSum; // normalize to sum = 1

			// 3) Redistribute total overflow according to these weights.
			for (int i = 0; i < 8; i++)
			{
				v[i] += weights[i] * totalOverflow;
			}

			return v;
		}


		/// <summary>
		/// Damps impulses channel-wise based on current level and per-channel damping.
		/// Positive impulses slow down near MAX_STIM.
		/// Negative impulses (satisfaction) are less damped and get stronger when level is high.
		/// </summary>
		private Vector8 DampStimulation(Vector8 impulses, Vector8 current)
		{
			Vector8 result = Vector8.Zero;

			for (int i = 0; i < 8; i++)
			{
				float impulse = impulses[i];
				if (Mathf.Approximately(impulse, 0f))
				{
					result[i] = 0f;
					continue;
				}

				float level = Mathf.Abs(current[i]);
				float damp = Mathf.Max(settings.StimDamping[i], 0f);

				if (damp <= 0f)
				{
					result[i] = impulse;
					continue;
				}

				float lowDamp = damp * 0.25f;

				if (impulse > 0f)
				{
					// Excitatory: same as before – fast under 1, heavily damped near MAX_STIM.
					float denom;
					if (level <= 1f)
					{
						denom = 1f + level * lowDamp;
					}
					else
					{
						float over = Mathf.Clamp(level - 1f, 0f, AEMOI.MAX_STIM - 1f);
						float overNorm = over / (AEMOI.MAX_STIM - 1f);
						denom = 1f + lowDamp + damp * overNorm * overNorm;
					}
					result[i] = impulse / denom;
				}
				else // impulse < 0f  => satisfaction / relaxation
				{
					// The higher the level, the stronger we allow negative impulses to pull it down.
					float levelNorm = Mathf.Clamp01(level / AEMOI.MAX_STIM);

					// Slight damping at low levels so tiny negatives don't jitter.
					float denomNeg = 1f + level * lowDamp * 0.25f;

					// Boost factor grows with level & damping: big emotions can be drained fast when safe.
					float boost = 1f + levelNorm * damp * 0.5f;

					result[i] = (impulse / denomNeg) * boost;
				}
			}

			return result;
		}
	}
}
