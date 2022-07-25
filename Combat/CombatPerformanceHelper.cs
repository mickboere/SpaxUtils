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

		public int Priority { get; }
		public List<string> SupportsActs { get; } = new List<string> { ActorActs.LIGHT, ActorActs.HEAVY };
		public float PerformanceTime { get; private set; }
		public bool Performing => !(Finishing || Completed);

		public ICombatMove Current { get; private set; }
		public CombatPerformanceState State { get; private set; }
		public float Charge { get; private set; }

		#region State getters
		public bool Charging => Current != null && State.HasFlag(CombatPerformanceState.Charging);
		public bool Attacking => Current != null && !State.HasFlag(CombatPerformanceState.Charging);
		public bool Released => Current != null && State.HasFlag(CombatPerformanceState.Released);
		public bool Finishing => Current != null && State.HasFlag(CombatPerformanceState.Finishing);
		public bool Completed => Current == null || State.HasFlag(CombatPerformanceState.Completed);
		#endregion

		private IAgent agent;
		private CallbackService callbackService;

		public CombatPerformanceHelper(ICombatMove move, IAgent agent, CallbackService callbackService, int prio = 0)
		{
			Priority = prio;
			Current = move;
			State = CombatPerformanceState.Charging;
			Charge = 0f;
			PerformanceTime = 0f;
			this.agent = agent;
			this.callbackService = callbackService;

			callbackService.UpdateCallback += Update;
		}

		public void Dispose()
		{
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
				return true;
			}

			// Auto complete current with minimum charge.
			return false;
		}

		private void Update()
		{
			EntityStat speedMult = Charging ? agent.GetStat(Current.ChargeSpeedMultiplier) : agent.GetStat(Current.PerformSpeedMultiplier);

			if (Charging)
			{
				Charge += Time.deltaTime * (speedMult ?? 1f);

				if (Released && Charge > Current.MinCharge)
				{
					// Finished charging.
					State = State.UnsetFlag(CombatPerformanceState.Charging);
				}
			}

			if (!Charging)
			{
				PerformanceTime += Time.deltaTime * (speedMult ?? 1f);
			}

			if (PerformanceTime > Current.MinDuration)
			{
				State = State.SetFlag(CombatPerformanceState.Finishing);
			}
			if (PerformanceTime > Current.TotalDuration)
			{
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
