using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Provides combatant with all data relevant to a weapon.
	/// </summary>
	public class WeaponComponent : EntityComponentBase
	{
		[SerializeField] private List<Collider> hitBoxes;

        //private void UpdateHitSweeping()
        //{
        //    if (hitSweeping)
        //    {
        //        if (!wasHitSweeping)
        //        {
        //            // Entered sweep state
        //            currentAttackHits.Clear();
        //        }

        //        if (AttackingState)
        //        {
        //            // Perform sweep for current frame
        //            List<HitScanHitData> hits = CombatUtils.PerformHitSweep(agent.Targetable.Center, sword.Size, lastSwordPos.position, lastSwordPos.rotation, sword.transform.position, sword.transform.rotation, 5, attackingMask);
        //            foreach (HitScanHitData hit in hits)
        //            {
        //                IHittable hittable = hit.Transform.GetComponentInParent<IHittable>();
        //                if (hittable != null && !currentAttackHits.Contains(hittable) && hit.Transform.root != transform.root)
        //                {
        //                    SpaxDebug.Log($"{agent.Identification.Name} - HIT: ({hittable.Entity.Identification.Name})");

        //                    currentAttackHits.Add(hittable);
        //                    HitData hitData = new HitData(Entity, new ImpactData(hit.Point, hit.Direction, hitForce));
        //                    HitEvent?.Invoke(hittable, hitData);
        //                    hittable.Hit(hitData);
        //                    PostHitEvent?.Invoke(hittable, hitData);
        //                }
        //            }
        //        }
        //        else
        //        {
        //            // Exited sweep state
        //            hitSweeping = false;
        //        }
        //    }

        //    wasHitSweeping = hitSweeping;
        //    lastSwordPos.position = sword.transform.position;
        //    lastSwordPos.rotation = sword.transform.rotation;
        //}
    }
}
