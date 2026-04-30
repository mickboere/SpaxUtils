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

		/// <inheritdoc/>
		public IEntity ActiveTarget { get; private set; }

		/// <inheritdoc/>
		public Vector8 Emotion { get; private set; }

		/// <inheritdoc/>
		public Vector8 Balance { get; private set; }

		private IDependencyManager dependencyManager;
		private AEMOISettings settings;
		private IOctad inclination;
		private IOctad personality;
		private List<IMindBehaviour> behaviours;

		private Dictionary<IEntity, Vector8> stimuli = new Dictionary<IEntity, Vector8>();
		private Dictionary<IEntity, Vector8> filters = new Dictionary<IEntity, Vector8>();
		private Vector8 emotionSmoothed;

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
			// 1. Gather senses.
			UpdatingEvent?.Invoke(delta);

			// 2. Per-entity overflow redistribution + decay + clamp.
			List<IEntity> sources = new List<IEntity>(stimuli.Keys);
			for (int s = 0; s < sources.Count; s++)
			{
				IEntity source = sources[s];
				Vector8 v = stimuli[source];
				v = RedistributeOverflow(v, delta);
				v = v.FILerp(Vector8.Zero, settings.EmotionDecay * delta);
				v = v.Clamp(-MAX_STIM, MAX_STIM);
				stimuli[source] = v;
			}

			// 3. True internal emotional state (unsigned aggregate, smoothed).
			Emotion = ComputeEmotion(delta);

			// 4. Most salient entity by absolute magnitude.
			Motivation = GetStrongestStimuli();

			// 5. Behaviour reassessment — sets ActiveBehaviour and ActiveTarget.
			ReassessBehaviour();

			// 6. Balance needs ActiveTarget from step 5.
			Balance = ComputeBalance();

			// 7. Fire MotivatedEvent AFTER ActiveTarget and Balance are up-to-date.
			MotivatedEvent?.Invoke();

			// 8. Mind fully updated.
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
				stimuli.Add(source, filtered(stimulation * Inclination).Clamp(-MAX_STIM, MAX_STIM));
			}
			else
			{
				stimuli[source] = (stimuli[source] + filtered(stimulation * Inclination)).Clamp(-MAX_STIM, MAX_STIM);
			}
		}

		/// <inheritdoc/>
		public void Satisfy(Vector8 satisfaction, IEntity source)
		{
			if (stimuli.ContainsKey(source))
			{
				stimuli[source] = stimuli[source].MoveTowardZero(satisfaction);
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
			if (ActiveBehaviour != null && !ActiveBehaviour.Interuptable)
			{
				return;
			}

			IEntity candidate = Motivation.target;
			Vector8 candidateStim = candidate != null && stimuli.TryGetValue(candidate, out Vector8 cs)
				? cs : Vector8.Zero;

			IMindBehaviour best = null;
			IEntity bestTarget = null;
			float bestStrength = 0f;

			IMindBehaviour active = ActiveBehaviour;
			float activeStrength = 0f;
			bool activeValid = false;

			for (int i = 0; i < behaviours.Count; i++)
			{
				IMindBehaviour behaviour = behaviours[i];
				var (t, strength) = behaviour.Evaluate(candidate, candidateStim);
				if (strength <= 0f)
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
					bestTarget = t;
					bestStrength = strength;
				}
			}

			if (best == null)
			{
				StopBehaviour();
				ActiveTarget = null;
				return;
			}

			// Same behaviour: update target and keep going.
			if (best == active)
			{
				ActiveTarget = bestTarget;
				return;
			}

			// Behaviour inertia: if priorities match, require a strength gap.
			if (active != null &&
				activeValid &&
				best.Priority == active.Priority &&
				settings.BehaviourSwitchThreshold > 0f)
			{
				if (bestStrength < activeStrength * (1f + settings.BehaviourSwitchThreshold))
				{
					return;
				}
			}

			StopBehaviour();
			ActiveTarget = bestTarget;
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
		/// Returns the stimuli with the highest absolute magnitude across all tracked entities.
		/// </summary>
		private (Vector8 stimuli, IEntity source) GetStrongestStimuli()
		{
			Vector8 motivation = Vector8.Zero;
			IEntity target = null;
			float highest = 0f;

			foreach (KeyValuePair<IEntity, Vector8> kvp in stimuli)
			{
				float mag = Mathf.Abs(kvp.Value.HighestAbs(out _));
				if (mag > highest)
				{
					motivation = kvp.Value;
					highest = mag;
					target = kvp.Key;
				}
			}

			return (motivation, target);
		}

		/// <summary>
		/// Computes the agent's true internal emotional state: unsigned, slow-smoothed average of |stim[i]| across all entities.
		/// </summary>
		private Vector8 ComputeEmotion(float delta)
		{
			Vector8 target = Vector8.Zero;
			if (stimuli.Count > 0)
			{
				foreach (KeyValuePair<IEntity, Vector8> kv in stimuli)
				{
					for (int i = 0; i < 8; i++)
					{
						target[i] += Mathf.Abs(kv.Value[i]);
					}
				}
				for (int i = 0; i < 8; i++)
				{
					target[i] /= stimuli.Count;
				}
			}
			return emotionSmoothed = emotionSmoothed.LerpClamped(target, settings.EmotionSmoothRate * delta);
		}

		/// <summary>
		/// Computes behavioural lean from Inclination + Personality + Emotion + directed stim toward ActiveTarget.
		/// Stored as a Vector8 where each pole holds its lean value (the losing pole is zero).
		/// </summary>
		private Vector8 ComputeBalance()
		{
			Vector8 inc        = inclination.Vector8;
			Vector8 per        = personality.Vector8;
			Vector8 emo        = Emotion;
			Vector8 targetStim = ActiveTarget != null && stimuli.TryGetValue(ActiveTarget, out Vector8 s)
				? s : Vector8.Zero;

			Vector8 balance = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				int opp = (i + 4) % 8;

				float poleStrength = inc[i]   * settings.BalanceInclinationWeight
				                   + per[i]   * settings.BalancePersonalityWeight
				                   + emo[i]   * settings.BalanceEmotionWeight;
				float oppStrength  = inc[opp] * settings.BalanceInclinationWeight
				                   + per[opp] * settings.BalancePersonalityWeight
				                   + emo[opp] * settings.BalanceEmotionWeight;
				float axisInertia  = poleStrength + oppStrength;

				float poleEmo = Mathf.Abs(targetStim[i])   / (1f + axisInertia * settings.BalanceInertiaK);
				float oppEmo  = Mathf.Abs(targetStim[opp]) / (1f + axisInertia * settings.BalanceInertiaK);

				float poleTotal = poleStrength + poleEmo;
				float oppTotal  = oppStrength  + oppEmo;
				float sum       = poleTotal + oppTotal;

				balance[i] = sum > 0.001f ? Mathf.Clamp01(poleTotal / sum) : 0.5f;
			}
			return balance;
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

			// 1) Clip to threshold and collect per-axis overflow (magnitude-gated, sign-preserving).
			Vector8 overflow = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				float abs = Mathf.Abs(v[i]);
				if (abs > settings.OverflowThreshold)
				{
					float extra = abs - settings.OverflowThreshold;
					float move = Mathf.Min(extra, extra * settings.OverflowRedistributionRate * delta);
					float sign = Mathf.Sign(v[i]);
					v[i] -= move * sign;
					overflow[i] = move * sign; // carry sign into overflow
				}
			}

			if (overflow.Sum() <= Mathf.Epsilon)
			{
				return v;
			}

			// 2) For each source axis, redistribute its overflow to other axes
			//    using personality and distance falloff. Sign of overflow propagates to sinks.
			for (int src = 0; src < 8; src++)
			{
				float amount = overflow[src]; // signed
				if (Mathf.Approximately(amount, 0f))
				{
					continue;
				}

				float absAmount = Mathf.Abs(amount);
				float sign = Mathf.Sign(amount);

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

				float scale = absAmount / sinkSum;
				for (int dst = 0; dst < 8; dst++)
				{
					float w = sinkWeights[dst];
					if (w > 0f)
					{
						v[dst] += w * scale * sign; // sign propagates to sinks
					}
				}
			}

			return v;
		}

		/// <summary>
		/// Damps impulses channel-wise based on current level and per-channel damping.
		/// Away-from-zero impulses (same sign as current) slow down near MAX_STIM.
		/// Toward-zero impulses (opposite sign to current) are shaped so that:
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
					result[i] = impulse;
					continue;
				}

				float lowDamp = damp * 0.25f;

				// Away from zero: same sign as current (or current is zero).
				bool awayFromZero = impulse * current[i] >= 0f || Mathf.Approximately(current[i], 0f);

				if (awayFromZero)
				{
					// Fast under 1, heavily damped near MAX_STIM.
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
					// Toward zero: satisfaction / relaxation.
					// Goal: dominant directions drain slowly, opposed drain quickly.
					// High levels are more "inert" (can't be erased instantly).

					float levelNorm = Mathf.Clamp01(level / MAX_STIM);
					float denomNeg = 1f + levelNorm * damp;

					float axisMult = 1f;

					if (settings.AxisBalanceSatisfactionStrength > 0f)
					{
						int opposite = (i + 4) % 8;
						float a = Mathf.Max(incl[i], 0f);
						float b = Mathf.Max(incl[opposite], 0f);
						float sum = a + b;

						if (sum > Mathf.Epsilon)
						{
							float axisBalance = (a - b) / sum;
							float t = (axisBalance + 1f) * 0.5f;

							float baseMult = Mathf.Lerp(
								settings.AxisBalanceSatisfactionRange.y,
								settings.AxisBalanceSatisfactionRange.x,
								t);

							axisMult = Mathf.Lerp(1f, baseMult, settings.AxisBalanceSatisfactionStrength);
						}
					}

					result[i] = (impulse / denomNeg) * axisMult;
				}
			}

			return result;
		}
	}
}
