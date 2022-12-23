using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="CombatPerformerComponent"/> performance helper class that supports playing out a single combat move performance.
	/// </summary>
	public class CombatPerformanceHelper : ICombatPerformer, IDisposable
	{
		public event IPerformer.PerformanceUpdateDelegate PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;

		#region Properties

		public int Priority { get; }
		public List<string> SupportsActs { get; } = new List<string> { ActorActs.LIGHT, ActorActs.HEAVY };
		public float PerformanceTime { get; private set; }
		public bool Performing => !(Finishing || Completed);

		public ICombatMove Current { get; private set; }
		public CombatPerformanceState State { get; private set; }
		public float Charge { get; private set; }

		#endregion // Properties

		#region State getters
		public bool Charging => Current != null && State.HasFlag(CombatPerformanceState.Charging);
		public bool Attacking => Current != null && !State.HasFlag(CombatPerformanceState.Charging);
		public bool Released => Current != null && State.HasFlag(CombatPerformanceState.Released);
		public bool Finishing => Current != null && State.HasFlag(CombatPerformanceState.Finishing);
		public bool Completed => Current == null || State.HasFlag(CombatPerformanceState.Completed);
		#endregion

		private IAgent agent;
		private EntityStat entityTimeScale;
		private CallbackService callbackService;
		private TransformLookup transformLookup;
		private Action<List<HitScanHitData>> onNewHitDetected;
		private LayerMask layerMask;

		private HitDetectionHelper hitDetectionHelper;

		public CombatPerformanceHelper(
			ICombatMove move, IAgent agent, EntityStat entityTimeScale,
			CallbackService callbackService, TransformLookup transformLookup,
			Action<List<HitScanHitData>> onNewHitDetected, LayerMask layerMask, int prio = 0)
		{
			Priority = prio;

			Current = move;
			State = CombatPerformanceState.Charging;
			Charge = 0f;
			PerformanceTime = 0f;

			this.agent = agent;
			this.entityTimeScale = entityTimeScale;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;
			this.onNewHitDetected = onNewHitDetected;
			this.layerMask = layerMask;

			callbackService.UpdateCallback += Update;
		}

		public void Dispose()
		{
			hitDetectionHelper?.Dispose();
			callbackService.UpdateCallback -= Update;
		}

		public bool TryProduce(IAct act, out IPerformer performer)
		{
			performer = null;
			SpaxDebug.Error("Helper automatically begins performance on creation.");
			return false;
		}

		public bool TryPerform()
		{
			if (Current == null || Released)
			{
				return false;
			}

			State = State.SetFlag(CombatPerformanceState.Released);

			if (Charging && Current.RequireMinCharge)
			{
				// Cancel current.
				State = State.UnsetFlag(CombatPerformanceState.Charging);
				Current = null;
			}

			// Auto complete current with minimum charge.
			return true;
		}

		public void AddCombatMove(string act, ICombatMove move, int prio)
		{
			SpaxDebug.Error("Combat performance helper does not support multiple moves.");
		}

		public void RemoveCombatMove(string act, ICombatMove move)
		{
			SpaxDebug.Error("Combat performance helper does not support multiple moves.");
		}

		private void Update()
		{
			EntityStat speedMult = Charging ? agent.GetStat(Current.ChargeSpeedMultiplierStat) : agent.GetStat(Current.PerformSpeedMultiplierStat);
			float delta = Time.deltaTime * (speedMult ?? 1f) * entityTimeScale;

			if (Charging)
			{
				// Charging
				Charge += delta;

				if (Released && Charge > Current.MinCharge)
				{
					// Finished charging.
					State = State.UnsetFlag(CombatPerformanceState.Charging);
					hitDetectionHelper = new HitDetectionHelper(agent, transformLookup, Current, layerMask);
				}
			}

			if (Attacking)
			{
				// Attacking
				PerformanceTime += delta;

				if (!Finishing && hitDetectionHelper.Update(out List<HitScanHitData> newHits))
				{
					onNewHitDetected(newHits);
				}

				// TODO: Clamp performance to MinDuration / Peak until agent is standing still.
				// This won't be necessary until the charge mechanic increases attack force.
			}

			if (!Finishing && PerformanceTime > Current.MinDuration)
			{
				// Finishing
				State = State.SetFlag(CombatPerformanceState.Finishing);
			}

			if (!Completed && PerformanceTime > Current.TotalDuration)
			{
				// Completed
				State = State.SetFlag(CombatPerformanceState.Completed);
			}

			PoseTransition pose = Current.Evaluate(Charge, PerformanceTime, out float weight);
			PerformanceUpdateEvent?.Invoke(this, new PoserStruct(new PoseInstructions(pose, 1f)), weight);

			// If the combat performance has fully completed, dispose of ourselves.
			if (Completed)
			{
				PerformanceCompletedEvent?.Invoke(this);
				Dispose();
			}
		}
	}
}
