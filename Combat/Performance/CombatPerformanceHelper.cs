using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Helper class for <see cref="CombatPerformerComponent"/> which performs a single <see cref="ICombatMove"/>.
	/// Implements <see cref="ICombatPerformer"/>.
	/// </summary>
	public class CombatPerformanceHelper : ICombatPerformer, IDisposable
	{
		public event Action<List<HitScanHitData>> NewHitDetectedEvent;
		public event Action<HitData> ProcessHitEvent;
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;
		public event Action<IPerformer, PoserStruct, float> PoseUpdateEvent;

		#region IPerformer Properties
		public int Priority { get; }
		public IAct Act { get; }
		public Performance State { get; private set; }
		public float RunTime { get; private set; }
		#endregion IPerformer Properties

		#region ICombatPerformer Properties
		public ICombatMove CurrentMove { get; private set; }
		public float Charge { get; private set; }
		#endregion ICombatPerformer Properties

		private IAgent agent;
		private EntityStat entityTimeScale;
		private CallbackService callbackService;
		private TransformLookup transformLookup;
		private LayerMask layerMask;

		private bool released;
		private CombatHitDetectionHelper hitDetectionHelper;

		public CombatPerformanceHelper(IAct act,
			ICombatMove move, IAgent agent, EntityStat entityTimeScale,
			CallbackService callbackService, TransformLookup transformLookup,
			LayerMask layerMask, int prio = 0)
		{
			Act = act;
			Priority = prio;
			State = Performance.Preparing;
			RunTime = 0f;

			CurrentMove = move;
			Charge = 0f;

			this.agent = agent;
			this.entityTimeScale = entityTimeScale;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;
			this.layerMask = layerMask;

			hitDetectionHelper = new CombatHitDetectionHelper(agent, transformLookup, CurrentMove, layerMask);

			callbackService.UpdateCallback += Update;
		}

		public void Dispose()
		{
			callbackService.UpdateCallback -= Update;
			hitDetectionHelper.Dispose();
		}

		/// <inheritdoc/>
		public bool SupportsAct(string act)
		{
			SpaxDebug.Error("Helper does not support any particular act.");
			return false;
		}

		/// <inheritdoc/>
		public bool TryPrepare(IAct act, out IPerformer performer)
		{
			performer = null;
			SpaxDebug.Error("Helper automatically begins performance on creation.");
			return false;
		}

		/// <inheritdoc/>
		public bool TryPerform()
		{
			if (released)
			{
				// Already performing.
				return false;
			}

			released = true;

			if (CurrentMove.RequireMinCharge && Charge < CurrentMove.MinCharge)
			{
				// Min charge not reached but required, cancel attack.
				TryCancel(false);
				return false;
			}

			// Auto complete current with minimum charge.
			return true;
		}

		/// <inheritdoc/>
		public bool TryCancel(bool force)
		{
			if (force || State == Performance.Preparing || State == Performance.Finishing)
			{
				State = Performance.Completed;
				return true;
			}
			else
			{
				return false;
			}
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
			EntityStat speedMult = State == Performance.Preparing ? agent.GetStat(CurrentMove.ChargeSpeedMultiplierStat) : agent.GetStat(CurrentMove.PerformSpeedMultiplierStat);
			float delta = Time.deltaTime * (speedMult ?? 1f) * entityTimeScale;

			if (State == Performance.Preparing)
			{
				// Charging
				Charge += delta;

				if (released && Charge > CurrentMove.MinCharge)
				{
					// Finished charging.
					State = Performance.Performing;
				}
			}
			else
			{
				// Attacking
				RunTime += delta;

				if (State == Performance.Performing && hitDetectionHelper.Update(out List<HitScanHitData> newHits))
				{
					NewHitDetectedEvent?.Invoke(newHits);
				}

				if (RunTime > CurrentMove.TotalDuration)
				{
					// Completed
					State = Performance.Completed;
				}
				else if (RunTime > CurrentMove.MinDuration)
				{
					// Finishing
					State = Performance.Finishing;
				}
			}

			PoseTransition pose = CurrentMove.Evaluate(Charge, RunTime, out float weight);
			PoseUpdateEvent?.Invoke(this, new PoserStruct(new PoseInstructions(pose, 1f)), weight);

			PerformanceUpdateEvent?.Invoke(this);

			if (State == Performance.Completed)
			{
				PerformanceCompletedEvent?.Invoke(this);
			}
		}
	}
}
