using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// AEMOI: Artificial Emotional Intelligence.
	/// IMind implementation that stores sensory stimuli as Vector8 per outside-IEntity.
	/// From these stimuli the agent's strongest motivation can be inferred to then be acted upon.
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
		public (Vector8 emotion, IEntity target) Motivation { get; private set; }

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
				return;
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
				return;
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
			for (int s = 0; s < sources.Count; s++)
			{
				IEntity source = sources[s];
				Vector8 v = stimuli[source];

				// 1) Overflow dispersion based on Personality and distance.
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

			// Apply per-channel damping, plus axis-aware satisfaction shaping.
			stimulation = DampStimulation(stimulation, current);

			Vector8 filtered(Vector8 stim)
			{
				if (filters.ContainsKey(source))
				{
					return stim * filters[source];
				}
				return stim;
			}

			if (!stimuli.ContainsKey(source))
			{
				stimuli.Add(source, filtered(stimulation * Inclination).Clamp(0f, MAX_STIM));
			}
			else
			{
				stimuli[source] = (stimuli[source] + filtered(stimulation * Inclination)).Clamp(0f, MAX_STIM);
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
			// No need to reassess the active behaviour here as that will be done next update loop anyway.
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
			// If the current behaviour is not interruptable, do not switch.
			if (ActiveBehaviour != null && !ActiveBehaviour.Interuptable)
			{
				return;
			}

			IMindBehaviour best = null;
			float bestStrength = 0f;

			IMindBehaviour active = ActiveBehaviour;
			float activeStrength = 0f;
			bool activeValid = false;

			Vector8 emo = Motivation.emotion;
			IEntity target = Motivation.target;

			for (int i = 0; i < behaviours.Count; i++)
			{
				IMindBehaviour behaviour = behaviours[i];
				if (!behaviour.Valid(emo, target, out float strength))
				{
					continue;
				}

				if (behaviour == active)
				{
					activeValid = true;
					activeStrength = strength;
				}

				if (best == null ||
					behaviour.Priority > best.Priority ||
					(behaviour.Priority == best.Priority && strength > bestStrength))
				{
					best = behaviour;
					bestStrength = strength;
				}
			}

			if (best == null)
			{
				StopBehaviour();
				return;
			}

			// Same behaviour: keep going.
			if (best == active)
			{
				return;
			}

			// Behaviour inertia: if priorities match, require a strength gap.
			if (active != null &&
				activeValid &&
				best.Priority == active.Priority &&
				settings.BehaviourSwitchThreshold > 0f)
			{
				float thresholdFactor = 1f + settings.BehaviourSwitchThreshold;
				if (bestStrength < activeStrength * thresholdFactor)
				{
					// New candidate is not strong enough to justify a switch.
					return;
				}
			}

			StopBehaviour();
			StartBehaviour(best);
		}

		private void StopBehaviour()
		{
			if (ActiveBehaviour == null)
			{
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
		/// using Personality and octagonal distance as weights. Below the threshold, nothing happens.
		/// </summary>
		private Vector8 RedistributeOverflow(Vector8 v, float delta)
		{
			if (settings.OverflowThreshold <= 0f || settings.OverflowRedistributionRate <= 0f)
			{
				return v;
			}

			// 1) Clip to threshold and collect per-axis overflow.
			Vector8 overflow = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				float level = v[i];
				if (level > settings.OverflowThreshold)
				{
					float extra = level - settings.OverflowThreshold;
					float move = Mathf.Min(extra, extra * settings.OverflowRedistributionRate * delta);
					v[i] -= move;
					overflow[i] = move;
				}
			}

			if (overflow.Sum() <= Mathf.Epsilon)
			{
				return v;
			}

			// 2) For each source axis, redistribute its overflow to other axes
			//    using personality and distance falloff.
			for (int src = 0; src < 8; src++)
			{
				float amount = overflow[src];
				if (amount <= 0f)
				{
					continue;
				}

				Vector8 sinkWeights = Vector8.Zero;
				float sinkSum = 0f;

				for (int dst = 0; dst < 8; dst++)
				{
					if (dst == src)
					{
						continue;
					}

					float baseW = personality.Vector8[dst];
					if (baseW <= 0f)
					{
						continue;
					}

					// Circular distance on the octagon: 0..4 steps.
					int step = Mathf.Abs(dst - src);
					int distSteps = step > 4 ? 8 - step : step; // 1..4

					float distFactor = 1f / (1f + Mathf.Max(0f, settings.OverflowDistanceBias) * distSteps);
					float w = baseW * distFactor;
					if (w <= 0f)
					{
						continue;
					}

					sinkWeights[dst] = w;
					sinkSum += w;
				}

				if (sinkSum <= Mathf.Epsilon)
				{
					continue;
				}

				float scale = amount / sinkSum;
				for (int dst = 0; dst < 8; dst++)
				{
					float w = sinkWeights[dst];
					if (w > 0f)
					{
						v[dst] += w * scale;
					}
				}
			}

			return v;
		}

		/// <summary>
		/// Damps impulses channel-wise based on current level and per-channel damping.
		/// Positive impulses slow down near MAX_STIM.
		/// Negative impulses (satisfaction) are shaped so that:
		/// - high levels are more inert (harder to drain in one go)
		/// - axis balance (this dir vs its opposite) can strongly slow or accelerate draining.
		/// </summary>
		private Vector8 DampStimulation(Vector8 impulses, Vector8 current)
		{
			Vector8 result = Vector8.Zero;
			Vector8 incl = inclination.Vector8;

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
					// No damping configured for this channel.
					result[i] = impulse;
					continue;
				}

				float lowDamp = damp * 0.25f;

				if (impulse > 0f)
				{
					// ---------------------------
					// POSITIVE: same idea as before.
					// Fast under 1, heavily damped near MAX_STIM.
					// ---------------------------
					float denom;
					if (level <= 1f)
					{
						denom = 1f + level * lowDamp;
					}
					else
					{
						float over = Mathf.Clamp(level - 1f, 0f, MAX_STIM - 1f);
						float overNorm = over / (MAX_STIM - 1f);
						denom = 1f + lowDamp + damp * overNorm * overNorm;
					}

					result[i] = impulse / denom;
				}
				else
				{
					// ---------------------------
					// NEGATIVE: satisfaction / relaxation.
					// Goal: dominant directions drain slowly, opposed drain quickly.
					// Also: high levels are more "inert" (can't be erased instantly).
					// ---------------------------

					float levelNorm = Mathf.Clamp01(level / MAX_STIM);

					// Base damping from magnitude: bigger level => smaller change per tick.
					// You can tweak the factor (here: 1f) to tune global "stickiness" of large emotions.
					float denomNeg = 1f + levelNorm * damp;

					float axisMult = 1f;

					if (settings.AxisBalanceSatisfactionStrength > 0f)
					{
						// Compare inclination on this axis vs its opposite.
						int opposite = (i + 4) % 8;
						float a = Mathf.Max(incl[i], 0f);
						float b = Mathf.Max(incl[opposite], 0f);
						float sum = a + b;

						if (sum > Mathf.Epsilon)
						{
							// axisBalance: -1..1   (-1 = opposite dominates, +1 = this axis dominates)
							float axisBalance = (a - b) / sum;
							float t = (axisBalance + 1f) * 0.5f; // 0..1

							// Map balance to [max, min] so:
							// - when opposite dominates, we use the *max* multiplier (drain fast)
							// - when this axis dominates, we use the *min* multiplier (drain slowly)
							float baseMult = Mathf.Lerp(
								settings.AxisBalanceSatisfactionRange.y,
								settings.AxisBalanceSatisfactionRange.x,
								t);

							// Blend toward 1 by strength. Strength=0 => no effect. Strength=1 => full baseMult.
							axisMult = Mathf.Lerp(1f, baseMult, settings.AxisBalanceSatisfactionStrength);
						}
					}

					// Negative impulse scaled by magnitude inertia and axis balance.
					result[i] = (impulse / denomNeg) * axisMult;
				}
			}

			return result;
		}
	}
}
