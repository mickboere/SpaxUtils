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
		public event Action<IPerformer> StartedPreparingEvent; // < Can never be listened to in this implementation because it would be invoked on construction.
		public event Action<IPerformer> StartedPerformingEvent;
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;

		#region IPerformer Properties

		public int Priority => 0;
		public IAct Act { get; }

		// All clock state lives in the sample; these are views onto it so the runtime and an editor
		// preview advance identical logic. See PerformanceClock.
		public PerformanceState State { get => sample.State; set => sample.State = value; }
		public float RunTime => sample.RunTime;
		public float Weight => sample.Weight;

		#endregion IPerformer Properties

		#region IMovePerformer Properties

		public IPerformanceMove Move { get; private set; }
		public float ChargeTime => sample.ChargeTime;

		/// <summary>Surfaces the live charge from whichever behaviour provides one (e.g. the melee swing); 1 when none.</summary>
		public float ChargeMultiplier
		{
			get
			{
				foreach (BehaviourAsset behaviour in behaviours)
				{
					if (behaviour is IChargeProvider charge)
					{
						return charge.ChargeMultiplier;
					}
				}
				return 1f;
			}
		}

		public bool Prolong { get; set; }
		public bool Paused { get; set; }
		public bool Canceled { get; private set; }
		public float CancelTime => sample.CancelTime;

		#endregion IMovePerformer Properties

		private IDependencyManager dependencyManager;
		private IAgent agent;
		private EntityStat entityTimeScale;
		private CallbackService callbackService;

		private List<BehaviourAsset> behaviours;
		private PerformanceSample sample;
		private bool released;
		private bool startedPerformance;

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
			sample = PerformanceSample.Create(move);
			Prolong = false;
			Paused = false;

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
				return true;
			}

			released = true;

			if (Move.RequireMinCharge && ChargeTime < Move.MinCharge)
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
			if (force || State == PerformanceState.Preparing || (State == PerformanceState.Performing && RunTime.Approx(0f)))
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
			EntityStat chargeSpeed = agent.Stats.GetStat(Move.ChargeSpeedMultiplierStat);
			EntityStat performSpeed = agent.Stats.GetStat(Move.PerformSpeedMultiplierStat);
			PerformanceClockInput input = PerformanceClockInput.Create(sample, Time.deltaTime,
				chargeSpeed ?? 1f, performSpeed ?? 1f, entityTimeScale, released, Prolong, Paused);

			if (!Canceled)
			{
				sample = PerformanceClock.AdvanceCharge(Move, sample, input);

				// Phases are stepped separately rather than via Advance so the started event still fires
				// BEFORE RunTime moves, and still within the frame charging completed.
				if (sample.State != PerformanceState.Preparing)
				{
					if (!startedPerformance)
					{
						StartedPerformingEvent?.Invoke(this);
						startedPerformance = true;
					}

					sample = PerformanceClock.AdvancePerformance(Move, sample, input);
				}
			}
			else
			{
				sample = PerformanceClock.AdvanceCancel(Move, sample, input);
			}

			UpdateBehaviours();

			PerformanceUpdateEvent?.Invoke(this);

			if (State == PerformanceState.Completed)
			{
				sample.Weight = 0f;
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
			// Behaviours run on the PERFORMANCE's clock, not the frame's. Handing them raw deltaTime let
			// every timer inside them count down at wall speed while the agent moved at entity timescale -
			// so at 0.2x a leap's travel timer expired before it had covered any ground.
			float delta = Time.deltaTime * entityTimeScale;

			foreach (BehaviourAsset behaviour in behaviours)
			{
				if (behaviour is IUpdatable updatable)
				{
					updatable.ExternalUpdate(delta);
				}
			}
		}

		#endregion Behaviours
	}
}
