using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Helper class for <see cref="CombatPerformer"/> which tracks a series of colliders to check for hits.
	/// </summary>
	public class CombatHitDetector : IDisposable
	{
		public const int DEFAULT_SCANS = 4;

		public List<HitScanHitData> Hits { get; private set; } = new List<HitScanHitData>();

		private IAgent agent;
		private LayerMask layerMask;

		private List<Collider> colliders = new List<Collider>();
		private Dictionary<Collider, (Vector3 pos, Quaternion rot)> orientations = new Dictionary<Collider, (Vector3 pos, Quaternion rot)>();

		public CombatHitDetector(IAgent agent, TransformLookup lookup, ICombatMove move, LayerMask layerMask)
		{
			this.agent = agent;
			this.layerMask = layerMask;

			foreach (string identifier in move.HitBoxes)
			{
				Transform transform = lookup.Lookup(identifier);
				if (transform != null)
				{
					Collider[] childColliders = transform.GetComponentsInChildren<Collider>();
					foreach (Collider collider in childColliders)
					{
						if (!colliders.Contains(collider))
						{
							colliders.Add(collider);
							orientations[collider] = (collider.transform.position, collider.transform.rotation);
						}
					}
				}
			}
		}

		public void Dispose()
		{
		}

		public bool Update(out List<HitScanHitData> newHits)
		{
			newHits = new List<HitScanHitData>();

			foreach (Collider collider in colliders)
			{
				List<HitScanHitData> hits = CombatUtils.ColliderScan(agent.Targetable.Center, collider, orientations[collider], DEFAULT_SCANS, layerMask);

				foreach (HitScanHitData hit in hits)
				{
					if (!hit.Transform.HasParent(agent.Transform) &&
						!newHits.Any(h => h.GameObject == hit.GameObject) &&
						!Hits.Any(h => h.GameObject == hit.GameObject))
					{
						newHits.Add(hit);
					}
				}

				orientations[collider] = (collider.transform.position, collider.transform.rotation);
			}

			Hits.AddRange(newHits);
			return newHits.Count > 0;
		}
	}
}
