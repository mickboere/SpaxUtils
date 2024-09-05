using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour for melee <see cref="IMeleeCombatMove"/>s that manages hit-detection during performance.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatBehaviour_MeleeSFX", menuName = "ScriptableObjects/Combat/MeleeSFXBehaviourAsset")]
	public class MeleeSFXBehaviourAsset : BasePerformanceMoveBehaviourAsset
	{
		private Pool<PooledAudioSource> audioPool;

		[SerializeField] private SFXData swingSFX;

		public void InjectDependencies(Pool<PooledAudioSource> audioPool)
		{
			this.audioPool = audioPool;
		}

		public override void Start()
		{
			base.Start();

			Performer.PerformanceStartedEvent += OnPerformanceStartedEvent;
		}

		public override void Stop()
		{
			base.Stop();

			Performer.PerformanceStartedEvent -= OnPerformanceStartedEvent;
		}

		protected void OnPerformanceStartedEvent(IPerformer performer)
		{
			swingSFX.Play(audioPool.Request(Agent.Targetable.Center).AudioSource);
		}
	}
}
