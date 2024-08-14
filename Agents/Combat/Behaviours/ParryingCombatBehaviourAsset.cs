using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Attacking behaviour that parries incoming attacks.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatBehaviour_Parrying", menuName = "ScriptableObjects/Combat/ParryingCombatBehaviourAsset")]
	public class ParryingCombatBehaviourAsset : MeleeCombatBehaviourAsset
	{
		private IHittable hittable;

		private List<IEntity> parries = new List<IEntity>();

		public void InjectDependencies(IHittable hittable)
		{
			this.hittable = hittable;
		}

		public override void Start()
		{
			base.Start();

			hittable.Subscribe(this, OnHitEvent, 1000);
		}

		public override void Stop()
		{
			base.Stop();

			hittable.Unsubscribe(this);
			parries.Clear();
		}

		private void OnHitEvent(HitData hitData)
		{
			// Hit by enemy attack during parry.

			//SpaxDebug.Log("<color=lime>Parried attack!</color>", $"State={State}");

			if (State == PerformanceState.Performing)
			{
				// Successful Parry.
				hitData.Result_Parried = true;
				parries.Add(hitData.Hitter);

				if (hitData.Hitter.TryGetEntityComponent<IHittable>(out IHittable parriedHittable))
				{
					// Generate parry hit.
					HitData parryHit = new HitData(
						parriedHittable,
						Agent,
						rigidbodyWrapper.Mass,
						hitData.Inertia,
						-hitData.Inertia.normalized,
						hitData.Mass,
						hitData.Strength,
						0f,
						0f);

					base.ProcessAttack(parriedHittable, parryHit);
				}
			}
		}

		protected override void ProcessAttack(IHittable hittable, HitData hitData)
		{
			// Parry attack hit an enemy.

			if (parries.Any((e) => e == hittable.Entity))
			{
				// Enemy has already been parried, don't process hit.
				return;
			}

			//SpaxDebug.Log("<color=green>Parry hit!</color>");

			parries.Add(hittable.Entity);
			base.ProcessAttack(hittable, hitData);
		}
	}
}
