using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class GuardPerformerComponent : EntityComponentBase, IPerformer
	{
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;
		public event Action<PoserStruct, float> PoseUpdateEvent;

		#region IPerformer properties

		public int Priority => 0;
		public List<string> SupportsActs => new List<string> { ActorActs.GUARD };
		public Performance State { get; private set; }
		public float RunTime { get; private set; }

		#endregion IPerformer properties

		[SerializeField] private PerformanceMove defaultGuardMove;

		private IAgent agent;

		private bool guarding;
		private float guardTime;

		public void InjectDependencies(IAgent agent)
		{
			this.agent = agent;
		}

		public bool TryPrepare(IAct act, out IPerformer performer)
		{
			performer = this;
			if (State == Performance.Performing)
			{
				return false;
			}

			State = Performance.Preparing;
			guarding = true;
			return true;
		}

		public bool TryPerform()
		{
			if (!guarding)
			{
				return false;
			}

			guarding = false;
			return true;
		}

		public bool TryCancel()
		{
			return false;
		}

		protected void Update()
		{
			if (State != Performance.Inactive)
			{
				EntityStat speedMult = State == Performance.Preparing ? agent.GetStat(defaultGuardMove.ChargeSpeedMultiplierStat) : agent.GetStat(defaultGuardMove.PerformSpeedMultiplierStat);
				float delta = Time.deltaTime * (speedMult ?? 1f) * EntityTimeScale;

				if (State == Performance.Preparing)
				{
					// Block
					guardTime += delta;

					if (!guarding && guardTime > defaultGuardMove.MinCharge)
					{
						// Enter parry.
						State = Performance.Performing;
					}
				}
				else
				{
					// Parry
					RunTime += delta;

					if (RunTime > defaultGuardMove.MinDuration)
					{
						// Finishing
						State = Performance.Finishing;
					}
					if (RunTime > defaultGuardMove.TotalDuration)
					{
						// Completed
						State = Performance.Completed;
					}
				}

				PoseTransition pose = defaultGuardMove.Evaluate(guardTime, RunTime, out float weight);
				PoseUpdateEvent?.Invoke(new PoserStruct(new PoseInstructions(pose, 1f)), weight);

				PerformanceUpdateEvent?.Invoke(this);

				if (State == Performance.Completed)
				{
					OnComplete();
				}
			}
		}

		private void OnComplete()
		{
			PerformanceCompletedEvent?.Invoke(this);
			State = Performance.Inactive;
			guardTime = 0f;
			RunTime = 0f;
		}
	}
}
