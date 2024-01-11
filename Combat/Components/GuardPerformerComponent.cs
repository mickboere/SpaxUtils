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

		public bool Guarding { get; private set; }

		[SerializeField] private PoseSequence defaultGuardPose;

		public void InjectDependencies()
		{
		}

		protected void Update()
		{

		}

		public bool TryPrepare(IAct act, out IPerformer performer)
		{
			performer = this;

			if (State == Performance.Performing)
			{
				return false;
			}

			Guarding = true;
			return true;
		}

		public bool TryPerform()
		{
			if (!Guarding)
			{
				return false;
			}



			return true;
		}

		public bool TryCancel()
		{
			return false;
		}
	}
}
