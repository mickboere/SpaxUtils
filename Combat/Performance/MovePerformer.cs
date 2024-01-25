﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Helper class for <see cref="MovePerformerComponent"/> which performs a single <see cref="IPerformanceMove"/>.
	/// Implements <see cref="IMovePerformer"/>.
	/// </summary>
	public class MovePerformer : IMovePerformer, IDisposable
	{
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;
		public event Action<IPerformer, PoserStruct, float> PoseUpdateEvent;

		#region IPerformer Properties

		public int Priority => 0;
		public IAct Act { get; }
		public PerformanceState State { get; private set; }
		public float RunTime { get; private set; }

		#endregion IPerformer Properties

		#region IMovePerformer Properties

		public IPerformanceMove Move { get; private set; }
		public float Charge { get; private set; }

		#endregion IMovePerformer Properties

		private IDependencyManager dependencyManager;
		private IAgent agent;
		private EntityStat entityTimeScale;
		private CallbackService callbackService;

		private List<BehaviourAsset> behaviours;
		private bool released;
		private bool canceled;
		private float cancelTime;

		public MovePerformer(IDependencyManager dependencyManager, IAct act,
			IPerformanceMove move, IAgent agent, EntityStat entityTimeScale,
			CallbackService callbackService)
		{
			// Initialize dependencies.
			this.dependencyManager = new DependencyManager(dependencyManager, $"CombatPerformance: {move.Name}");
			this.dependencyManager.Bind(this);
			this.dependencyManager.Bind(move);

			// Initialize variables.
			this.agent = agent;
			this.entityTimeScale = entityTimeScale;
			this.callbackService = callbackService;
			Act = act;
			Move = move;
			State = Move.HasCharge ? PerformanceState.Preparing : PerformanceState.Performing;
			RunTime = 0f;
			Charge = 0f;

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

			if (Move.RequireMinCharge && Charge < Move.MinCharge)
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
			if (force || Act.Interuptable || State == PerformanceState.Preparing)
			{
				State = PerformanceState.Finishing;
				canceled = true;
				return true;
			}
			else
			{
				return false;
			}
		}

		public void AddMove(string act, object owner, PerformanceState state, IPerformanceMove move, int prio)
		{
			SpaxDebug.Error("MovePerformer does not support multiple moves.");
		}

		public void RemoveMove(string act, object owner)
		{
			SpaxDebug.Error("MovePerformer does not support multiple moves.");
		}

		private void Update()
		{
			EntityStat speedMult = State == PerformanceState.Preparing ? agent.GetStat(Move.ChargeSpeedMultiplierStat) : agent.GetStat(Move.PerformSpeedMultiplierStat);
			float delta = Time.deltaTime * (speedMult ?? 1f) * entityTimeScale;

			if (canceled)
			{
				cancelTime += delta;

				if (cancelTime >= Move.CancelDuration)
				{
					// Completed cancel fadeout.
					State = PerformanceState.Completed;
				}
			}
			else
			{
				if (State == PerformanceState.Preparing)
				{
					// Preparing.
					Charge += delta;

					if (released && Charge >= Move.MinCharge)
					{
						// Finished charging.
						State = PerformanceState.Performing;
					}
				}
				// No else statement to remove unnecessary frame delay.
				if (State != PerformanceState.Preparing)
				{
					// Performing.
					RunTime += delta;

					if (RunTime >= Move.TotalDuration)
					{
						// Completed
						State = PerformanceState.Completed;
					}
					else if (RunTime >= Move.MinDuration)
					{
						// Finishing
						State = PerformanceState.Finishing;
					}
				}
			}

			PoseTransition pose = Move.Evaluate(Charge, RunTime, out float weight, cancelTime);
			PoseUpdateEvent?.Invoke(this, new PoserStruct(new PoseInstructions(pose, 1f)), weight);

			UpdateBehaviour();

			PerformanceUpdateEvent?.Invoke(this);

			if (State == PerformanceState.Completed)
			{
				PerformanceCompletedEvent?.Invoke(this);
			}
		}

		private void StartBehaviour()
		{
			foreach (BehaviourAsset behaviour in Move.Behaviour)
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

		private void UpdateBehaviour()
		{
			foreach (BehaviourAsset behaviour in behaviours)
			{
				if (behaviour is IUpdatable updatable)
				{
					updatable.ExUpdate(Time.deltaTime);
				}
			}
		}
	}
}
