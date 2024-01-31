using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour for melee <see cref="ICombatMove"/>s that manages hit-detection during performance.
	/// </summary>
	[CreateAssetMenu(fileName = "Behaviour_Blocking", menuName = "ScriptableObjects/Combat/BlockingBehaviourAsset")]
	public class BlockingBehaviourAsset : BasePerformanceMoveBehaviourAsset
	{
		private IHittable hittable;

		public void InjectDependencies(IHittable hittable)
		{
			this.hittable = hittable;
		}

		public override void Start()
		{
			base.Start();
			hittable.Subscribe(this, OnHitEvent, 10);
		}

		public override void Stop()
		{
			base.Stop();
			hittable.Unsubscribe(this);
		}

		private void OnHitEvent(HitData hitData)
		{
			// TODO: Just increase defence when blocking.
			
		}
	}
}
