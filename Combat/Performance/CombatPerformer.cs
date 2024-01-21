using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Helper class for <see cref="CombatPerformerComponent"/> which performs a single <see cref="ICombatMove"/>.
	/// Implements <see cref="ICombatPerformer"/>.
	/// </summary>
	public class CombatPerformer : ICombatPerformer, IDisposable
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

		private IDependencyManager dependencyManager;
		private IAgent agent;
		private EntityStat entityTimeScale;
		private CallbackService callbackService;

		private bool released;
		private CombatHitDetector hitDetector;
		private List<BehaviourAsset> behaviours;

		public CombatPerformer(IDependencyManager dependencyManager, IAct act,
			ICombatMove move, IAgent agent, EntityStat entityTimeScale,
			CallbackService callbackService, TransformLookup transformLookup,
			LayerMask layerMask, int prio = 0)
		{
			// Initialize dependencies.
			this.dependencyManager = new DependencyManager(dependencyManager, $"CombatPerformance: {move.Name}");
			this.dependencyManager.Bind(this);
			this.dependencyManager.Bind(move);

			// Initialize variables.
			Act = act;
			CurrentMove = move;
			Priority = prio;
			this.agent = agent;
			this.entityTimeScale = entityTimeScale;
			this.callbackService = callbackService;
			State = Performance.Preparing;
			RunTime = 0f;
			Charge = 0f;

			// Initialize hit detector.
			hitDetector = new CombatHitDetector(agent, transformLookup, CurrentMove, layerMask);

			// Initialize behaviours.
			behaviours = new List<BehaviourAsset>();
			StartBehaviour();

			callbackService.UpdateCallback += Update;
		}

		public void Dispose()
		{
			dependencyManager.Dispose();
			StopBehaviour();
			callbackService.UpdateCallback -= Update;
			hitDetector.Dispose();
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
				// Charging.
				Charge += delta;

				if (released && Charge >= CurrentMove.MinCharge)
				{
					// Finished charging.
					State = Performance.Performing;
				}
			}
			else
			{
				// Attacking.
				RunTime += delta;

				// Hit detection.
				if (State == Performance.Performing && RunTime >= CurrentMove.HitDetectionDelay && hitDetector.Update(out List<HitScanHitData> newHits))
				{
					NewHitDetectedEvent?.Invoke(newHits);
				}

				if (RunTime >= CurrentMove.TotalDuration)
				{
					// Completed
					State = Performance.Completed;
				}
				else if (RunTime >= CurrentMove.MinDuration)
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

		private void StartBehaviour()
		{
			foreach (BehaviourAsset behaviour in CurrentMove.Behaviour)
			{
				BehaviourAsset behaviourInstance = behaviour.CreateInstance();
				behaviours.Add(behaviourInstance);
				dependencyManager.Inject(behaviourInstance);
				behaviourInstance.Start();
			}
		}

		private void StopBehaviour()
		{
			foreach (BehaviourAsset behaviour in behaviours)
			{
				behaviour.Destroy();
			}
		}
	}
}
