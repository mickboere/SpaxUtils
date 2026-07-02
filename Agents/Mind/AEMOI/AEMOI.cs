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
		public Vector8 Inclination => inclination;

		/// <inheritdoc/>
		public Vector8 Personality => personality;

		/// <inheritdoc/>
		public IReadOnlyDictionary<IEntity, Vector8> Stimuli => motivations;

		/// <inheritdoc/>
		public (Vector8 emotion, IEntity target) Motivation { get; private set; }

		/// <inheritdoc/>
		public IMindBehaviour ActiveBehaviour { get; private set; }

		/// <inheritdoc/>
		public IEntity ActiveTarget { get; private set; }

		/// <inheritdoc/>
		public Vector8 Emotion { get; private set; }

		/// <inheritdoc/>
		public Vector8 EmotionNormalized { get; private set; }

		/// <inheritdoc/>
		public Vector8 Balance { get; private set; }

		private IDependencyManager dependencyManager;
		private AEMOISettings settings;
		private Vector8 inclination;
		private Vector8 personality;
		private List<IMindBehaviour> behaviours;

		// Persistent per-foe Stimulation reservoir (uncapped except hard ±MAX); tracks toward Demand + receives impulses.
		private Dictionary<IEntity, Vector8> stimulation = new Dictionary<IEntity, Vector8>();
		// Per-frame Demand accumulator (summed within a tick, cleared at its end).
		private Dictionary<IEntity, Vector8> demandBuffer = new Dictionary<IEntity, Vector8>();
		// Behaviour-facing per-foe Motivation: Stimulation clamped under the Emotion envelope. Recomputed each tick.
		private Dictionary<IEntity, Vector8> motivations = new Dictionary<IEntity, Vector8>();
		// Internal raw Emotion state (the slow envelope) that the public Emotion is published from.
		private Vector8 emotionState;

		public AEMOI(IDependencyManager dependencyManager, AEMOISettings settings, Vector8 inclination, Vector8 personality, IEnumerable<IMindBehaviour> behaviours = null)
		{
			this.dependencyManager = dependencyManager;
			this.settings = settings;
			this.inclination = inclination;
			this.personality = personality;
			this.behaviours = behaviours == null ? new List<IMindBehaviour>() : new List<IMindBehaviour>(behaviours);
		}

		/// <summary>
		/// Sets the (static snapshot) Inclination and Personality traits. Called once after the agent's stats have
		/// initialized, since the stat distributions these derive from aren't available at construction time.
		/// </summary>
		public void SetTraits(Vector8 inclination, Vector8 personality)
		{
			this.inclination = inclination;
			this.personality = personality;
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
				stimulation.Clear();
				demandBuffer.Clear();
				motivations.Clear();
				emotionState = Vector8.Zero;
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
			// A deactivated mind must never tick (e.g. a stray pump after the agent dies but before its state exits).
			if (!Active)
			{
				return;
			}

			// Clear last tick's Demand at the START (not the end) so it survives the tick for debug/HUD inspection.
			demandBuffer.Clear();

			// 1. Gather senses: continuous senses fill demandBuffer via SetDemand; impulses/Satisfy hit Stimulation.
			UpdatingEvent?.Invoke(delta);

			// 2. Track Stimulation toward Demand per foe, per axis, with an inclination-asymmetric rate: strong-inclination
			//    axes rise fast + fall slow (the drive builds and lingers = retentive/grudge → it dominates selection);
			//    weak axes rise slow + fall fast (they leak between bursts and never accumulate → stay suppressed).
			//    Also aggregate the Emotion envelope target as MAX |Stimulation| across foes (highest threat sets arousal).
			Vector8 incl = inclination;
			float bias = Mathf.Clamp01(settings.StimulationInclinationBias);
			Vector8 emotionTarget = Vector8.Zero;
			List<IEntity> sources = new List<IEntity>(stimulation.Keys);
			foreach (IEntity source in demandBuffer.Keys)
			{
				if (!stimulation.ContainsKey(source))
				{
					sources.Add(source);
				}
			}
			for (int s = 0; s < sources.Count; s++)
			{
				IEntity source = sources[s];
				Vector8 demand = demandBuffer.TryGetValue(source, out Vector8 d) ? d : Vector8.Zero;
				Vector8 stim = stimulation.TryGetValue(source, out Vector8 st) ? st : Vector8.Zero;

				for (int i = 0; i < 8; i++)
				{
					float inc = Mathf.Clamp01(incl[i]);
					// Rising = drive growing toward a stronger demand; falling = demand dropped below the current drive.
					bool rising = Mathf.Abs(demand[i]) >= Mathf.Abs(stim[i]);
					float incFactor = rising
						? Mathf.Lerp(1f - bias, 1f, inc)   // strong → full rise, weak → damped
						: Mathf.Lerp(1f, 1f - bias, inc);  // strong → damped fall (lingers), weak → full fall (leaks)
					float rate = settings.StimulationRate * incFactor;
					stim[i] = Mathf.Lerp(stim[i], demand[i], 1f - Mathf.Exp(-Mathf.Max(rate, 0f) * delta));
				}
				stim = RedistributeOverflow(stim, delta).Clamp(-MAX_STIM, MAX_STIM);
				stimulation[source] = stim;

				for (int i = 0; i < 8; i++)
				{
					float a = Mathf.Abs(stim[i]);
					if (a > emotionTarget[i])
					{
						emotionTarget[i] = a;
					}
				}
			}

			// 3. Emotion follower: uniform symmetric envelope chasing |Stimulation| at EmotionRate (the softcap ramp).
			Emotion = ComputeEmotion(emotionTarget, delta);
			EmotionNormalized = NormalizeEmotion(Emotion);

			// 4. Derive behaviour-facing Motivation per foe: Stimulation clamped under the Emotion envelope.
			Vector8 cap = (Emotion + Vector8.One * settings.BaseFloor).Clamp(0f, MAX_STIM);
			motivations.Clear();
			for (int s = 0; s < sources.Count; s++)
			{
				IEntity source = sources[s];
				motivations[source] = stimulation[source].ClampMagnitude(cap);
			}

			// 5. Most salient entity by absolute magnitude (from clamped Motivation).
			Motivation = GetStrongestStimuli();

			// 6. Behaviour reassessment — sets ActiveBehaviour and ActiveTarget.
			ReassessBehaviour();

			// 7. Balance needs ActiveTarget from step 6.
			Balance = ComputeBalance();

			// 8. Fire MotivatedEvent AFTER ActiveTarget and Balance are up-to-date.
			MotivatedEvent?.Invoke();

			// 9. Mind fully updated. (Demand buffer is cleared at the start of the next tick.)
			UpdatedEvent?.Invoke();
		}

		#endregion Activity

		#region Stimulation

		/// <inheritdoc/>
		public void SetDemand(Vector8 demand, IEntity source)
		{
			// Continuous input: sum contributions from all sources within the frame; the buffer resets each tick.
			// Clamped to ±MAX so Demand stays a true level — an overshooting target would let the tracker cover more
			// than the distance to the real ceiling, saturating Stimulation faster than the StimulationRate·inclination
			// rate intends (which would let big threats bypass the difficulty-remapped rate-gate).
			Vector8 sum = demandBuffer.TryGetValue(source, out Vector8 existing) ? existing + demand : demand;
			demandBuffer[source] = sum.Clamp(-MAX_STIM, MAX_STIM);
		}

		/// <inheritdoc/>
		public void Stimulate(Vector8 stimulation, IEntity source)
		{
			// Impulse input: add straight into the persistent Stimulation reservoir (hard-clamped ±MAX). Bounded by
			// the Emotion envelope downstream in Motivation, so it builds the response rather than bypassing the cap.
			Vector8 current = this.stimulation.TryGetValue(source, out Vector8 existing) ? existing : Vector8.Zero;
			this.stimulation[source] = (current + stimulation).Clamp(-MAX_STIM, MAX_STIM);
		}

		/// <inheritdoc/>
		public void Satisfy(Vector8 satisfaction, IEntity source)
		{
			if (stimulation.ContainsKey(source))
			{
				stimulation[source] = stimulation[source].MoveTowardZero(satisfaction);
			}
		}

		/// <inheritdoc/>
		public void ClearStimuli(IEntity source)
		{
			stimulation.Remove(source);
			demandBuffer.Remove(source);
			motivations.Remove(source);
		}

		/// <inheritdoc/>
		public Vector8 RetrieveStimuli(IEntity source)
		{
			return motivations.TryGetValue(source, out Vector8 m) ? m : Vector8.Zero;
		}

		/// <inheritdoc/>
		public Vector8 RetrieveDemand(IEntity source)
		{
			return demandBuffer.TryGetValue(source, out Vector8 d) ? d : Vector8.Zero;
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

			IMindBehaviour best = null;
			IEntity bestTarget = null;
			float bestStrength = 0f;

			IMindBehaviour active = ActiveBehaviour;
			IEntity activeBestTarget = null;
			float activeBestStrength = 0f;

			// Evaluate every behaviour against every stimulated entity so that
			// ally-directed and enemy-directed behaviours can each find their own best candidate,
			// rather than all competing over the single globally-strongest entity.
			List<IEntity> candidates = new(motivations.Keys);
			for (int c = 0; c < candidates.Count; c++)
			{
				IEntity candidate = candidates[c];
				Vector8 candidateStim = motivations[candidate];

				for (int i = 0; i < behaviours.Count; i++)
				{
					IMindBehaviour behaviour = behaviours[i];
					var (t, strength) = behaviour.Evaluate(candidate, candidateStim);
					if (strength <= 0f)
					{
						continue;
					}

					if (behaviour == active && strength > activeBestStrength)
					{
						activeBestStrength = strength;
						activeBestTarget = t;
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
			}

			if (best == null)
			{
				StopBehaviour();
				ActiveTarget = null;
				return;
			}

			// Same behaviour wins again: update to its best-matching candidate this frame.
			if (best == active)
			{
				ActiveTarget = activeBestTarget;
				return;
			}

			// Behaviour inertia: if priorities match, require a strength gap to switch.
			if (active != null &&
				activeBestStrength > 0f &&
				best.Priority == active.Priority &&
				settings.BehaviourSwitchThreshold > 0f)
			{
				if (bestStrength < activeBestStrength * (1f + settings.BehaviourSwitchThreshold))
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
		/// Returns the clamped Motivation with the highest absolute magnitude across all tracked entities.
		/// </summary>
		private (Vector8 stimuli, IEntity source) GetStrongestStimuli()
		{
			Vector8 motivation = Vector8.Zero;
			IEntity target = null;
			float highest = 0f;

			foreach (KeyValuePair<IEntity, Vector8> kvp in motivations)
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
		/// Advances the internal Emotion envelope toward <paramref name="target"/> (the MAX |Stimulation| across foes),
		/// inclination-MIRRORED: strong-inclination axes RISE fast and FALL slow (build & linger), weak axes rise slow and
		/// fall fast (barely build, leak). So emotion only accumulates on the axes the agent actually cares about — a weak
		/// axis can't crest alongside its strong opposite and flatten Balance toward 0.5. Framerate-independent per axis.
		/// </summary>
		private Vector8 ComputeEmotion(Vector8 target, float delta)
		{
			Vector8 incl = inclination;
			float bias = Mathf.Clamp01(settings.EmotionInclinationBias);
			Vector8 result = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				float cur = emotionState[i];
				float tgt = target[i];
				float inc = Mathf.Clamp01(incl[i]);
				float rate = tgt > cur
					? settings.EmotionRate * Mathf.Lerp(1f - bias, 1f, inc)   // rise: strong fast, weak slow
					: settings.EmotionRate * Mathf.Lerp(1f, 1f - bias, inc);  // fall: strong slow, weak fast
				result[i] = Mathf.Lerp(cur, tgt, 1f - Mathf.Exp(-Mathf.Max(rate, 0f) * delta));
			}
			return emotionState = result;
		}

		/// <summary>
		/// Maps the raw emotion aggregate (unsigned, [0, MAX_STIM]) onto a normalized [0,1] range using a concave
		/// power curve. A just-actionable emotion (raw 1) maps to settings.EmotionNormalizationAnchor rather than the
		/// 0.1 a linear map would give it, so actionable emotions carry real weight while the high end saturates at 1.
		/// </summary>
		private Vector8 NormalizeEmotion(Vector8 raw)
		{
			float k = EmotionCurveExponent();
			Vector8 result = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				result[i] = CurveEmotion(raw[i], k);
			}
			return result;
		}

		/// <summary>
		/// Exponent for the emotion normalization curve, derived from the configured anchor such that
		/// curve(raw 1) == anchor and curve(MAX_STIM) == 1. anchor in (0.1, 1) yields k &lt; 1 (the intended concave shape).
		/// </summary>
		private float EmotionCurveExponent()
		{
			float anchor = Mathf.Clamp(settings.EmotionNormalizationAnchor, 0.0001f, 0.9999f);
			// Solve pow(1 / MAX_STIM, k) == anchor  ->  k = ln(anchor) / ln(1 / MAX_STIM).
			return Mathf.Log(anchor) / Mathf.Log(1f / MAX_STIM);
		}

		/// <summary>
		/// Applies the normalization curve to a single unsigned magnitude in [0, MAX_STIM], returning a value in [0,1].
		/// </summary>
		private float CurveEmotion(float magnitude, float k)
		{
			if (magnitude <= 0f)
			{
				return 0f;
			}
			return Mathf.Pow(Mathf.Clamp01(magnitude / MAX_STIM), k);
		}

		/// <summary>
		/// Computes behavioural lean from Inclination + Personality + Emotion + directed stim toward ActiveTarget.
		/// Stored as a Vector8 where each pole holds its lean value (the losing pole is zero).
		/// </summary>
		private Vector8 ComputeBalance()
		{
			Vector8 inc = inclination;
			Vector8 per = personality;
			Vector8 emo = EmotionNormalized;

			// Directed motivation toward the active target, curved onto the same normalized [0,1] scale as emo
			// so the directed term doesn't re-introduce the [0,MAX_STIM] imbalance the aggregate just shed.
			Vector8 rawTargetStim = ActiveTarget != null && motivations.TryGetValue(ActiveTarget, out Vector8 s)
				? s : Vector8.Zero;
			float curveK = EmotionCurveExponent();
			Vector8 targetStim = Vector8.Zero;
			for (int t = 0; t < 8; t++)
			{
				targetStim[t] = CurveEmotion(Mathf.Abs(rawTargetStim[t]), curveK);
			}

			Vector8 balance = Vector8.Zero;
			for (int i = 0; i < 8; i++)
			{
				int opp = (i + 4) % 8;

				float poleStrength = inc[i] * settings.BalanceInclinationWeight
								   + per[i] * settings.BalancePersonalityWeight
								   + emo[i] * settings.BalanceEmotionWeight;
				float oppStrength = inc[opp] * settings.BalanceInclinationWeight
								   + per[opp] * settings.BalancePersonalityWeight
								   + emo[opp] * settings.BalanceEmotionWeight;
				float axisInertia = poleStrength + oppStrength;

				// targetStim is already an unsigned curved magnitude in [0,1].
				float poleEmo = targetStim[i] / (1f + axisInertia * settings.BalanceInertiaK);
				float oppEmo = targetStim[opp] / (1f + axisInertia * settings.BalanceInertiaK);

				float poleTotal = poleStrength + poleEmo;
				float oppTotal = oppStrength + oppEmo;
				float sum = poleTotal + oppTotal;

				balance[i] = sum > 0.001f ? Mathf.Clamp01(poleTotal / sum) : 0.5f;
				// Lean: sharpen the deviation from neutral. Symmetric around 0.5, so the axis and its opposite still sum to 1.
				balance[i] = Mathf.Clamp01(0.5f + (balance[i] - 0.5f) * settings.BalanceLean);
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

					float baseW = personality[dst];
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
	}
}
