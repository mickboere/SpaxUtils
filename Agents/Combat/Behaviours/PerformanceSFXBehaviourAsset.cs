using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour for melee <see cref="IMeleeCombatMove"/>s that manages hit-detection during performance.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatBehaviour_PerformanceSFX", menuName = "ScriptableObjects/Combat/" + nameof(PerformanceSFXBehaviourAsset))]
	public class PerformanceSFXBehaviourAsset : BasePerformanceMoveBehaviourAsset
	{
		private Pool<PooledAudioSource> audioPool;

		[SerializeField, HideInInspector] private bool onPrepare;
		[SerializeField, Conditional(nameof(onPrepare), false, true, false)] private SFXData prepareSFX;
		[SerializeField, HideInInspector] private bool onPerform;
		[SerializeField, Conditional(nameof(onPerform), false, true, false)] private SFXData performSFX;

		public void InjectDependencies(Pool<PooledAudioSource> audioPool)
		{
			this.audioPool = audioPool;
		}

		public override void Start()
		{
			base.Start();

			if (onPrepare)
			{
				prepareSFX.Play(audioPool.Request(Agent.Targetable.Center).AudioSourceWrapper);
			}

			if (onPerform)
			{
				Performer.PerformanceStartedEvent += OnPerformanceStartedEvent;
			}
		}

		public override void Stop()
		{
			base.Stop();

			if (onPerform)
			{
				Performer.PerformanceStartedEvent -= OnPerformanceStartedEvent;
			}
		}

		protected void OnPerformanceStartedEvent(IPerformer performer)
		{
			performSFX.Play(audioPool.Request(Agent.Targetable.Center).AudioSourceWrapper);
		}
	}
}
