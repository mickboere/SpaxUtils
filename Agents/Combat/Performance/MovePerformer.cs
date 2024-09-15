using System;
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
		public event Action<IPerformer> PerformanceStartedEvent;
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;

		#region IPerformer Properties

		public int Priority => 0;
		public IAct Act { get; }
		public PerformanceState State { get; set; }
		public float RunTime { get; private set; }

		#endregion IPerformer Properties

		#region IMovePerformer Properties

		public IPerformanceMove Move { get; private set; }
		public float Charge { get; private set; }
		public bool Canceled { get; private set; }
		public float CancelTime { get; private set; }

		#endregion IMovePerformer Properties

		private IDependencyManager dependencyManager;
		private IAgent agent;
		private EntityStat entityTimeScale;
		private CallbackService callbackService;

		private List<BehaviourAsset> behaviours;
		private bool released;

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
			StartBehaviours();

			callbackService.UpdateCallback += Update;
		}

		public void Dispose()
		{
			dependencyManager.Dispose();
			StopBehaviours();
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
			if (!Move.HasCharge || released)
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
			if (force || State == PerformanceState.Preparing)
			{
				// Delay setting state to "Finishing" to prevent state change during Followup Move.
				Canceled = true;
				return true;
			}
			else
			{
				return false;
			}
		}

		private void Update()
		{
			if (!Canceled)
			{
				EntityStat speedMult = State == PerformanceState.Preparing ? agent.GetStat(Move.ChargeSpeedMultiplierStat) : agent.GetStat(Move.PerformSpeedMultiplierStat);
				float delta = Time.deltaTime * (speedMult ?? 1f) * entityTimeScale;

				if (State == PerformanceState.Preparing)
				{
					// Preparing (charging).
					Charge += delta;

					if (Charge >= Move.MinCharge && released)
					{
						// Finished charging.
						State = PerformanceState.Performing;
					}
				}
				// No else statement here to remove frame delay.
				if (State != PerformanceState.Preparing)
				{
					if (RunTime.Approx(0f))
					{
						PerformanceStartedEvent?.Invoke(this);
					}

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
			else
			{
				State = PerformanceState.Finishing;

				CancelTime += Time.deltaTime * entityTimeScale;

				if (CancelTime >= Move.CancelDuration)
				{
					// Completed cancel fadeout.
					State = PerformanceState.Completed;
				}
			}

			UpdateBehaviours();

			PerformanceUpdateEvent?.Invoke(this);

			if (State == PerformanceState.Completed)
			{
				PerformanceCompletedEvent?.Invoke(this);
			}
		}

		#region Behaviours

		private void StartBehaviours()
		{
			foreach (BehaviourAsset behaviour in Move.Behaviour)
			{
				BehaviourAsset behaviourInstance = behaviour.CreateInstance();
				behaviours.Add(behaviourInstance);
				dependencyManager.Inject(behaviourInstance);
				behaviourInstance.Start();
			}
		}

		private void StopBehaviours()
		{
			foreach (BehaviourAsset behaviour in behaviours)
			{
				behaviour.Destroy();
			}
		}

		private void UpdateBehaviours()
		{
			foreach (BehaviourAsset behaviour in behaviours)
			{
				if (behaviour is IUpdatable updatable)
				{
					updatable.ExternalUpdate(Time.deltaTime);
				}
			}
		}

		#endregion Behaviours
	}
}
