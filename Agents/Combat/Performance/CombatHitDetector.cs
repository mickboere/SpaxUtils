using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Helper class <see cref="IMeleeCombatMove"/> behaviours which tracks a series of colliders to check for hits.
	/// </summary>
	public class CombatHitDetector : IDisposable
	{
		// Upper clamp for the per-frame adaptive sub-scan count; CombatUtils.ColliderScan computes just enough
		// to fill the gap between frames and only hits this ceiling on very fast swings.
		public const int DEFAULT_SCANS = 16;

		public List<HitScanHitData> Hits { get; private set; } = new List<HitScanHitData>();

		private IAgent agent;
		private LayerMask layerMask;

		private List<Collider> colliders = new List<Collider>();
		private Dictionary<Collider, (Vector3 pos, Quaternion rot)> orientations = new Dictionary<Collider, (Vector3 pos, Quaternion rot)>();

		public CombatHitDetector(IAgent agent, TransformLookup lookup, IMeleeCombatMove move, LayerMask layerMask)
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

		/// <summary>
		/// Ticks the detector. When <paramref name="scan"/> is true it sweeps each collider from its previous
		/// orientation to its current one and reports new hits; when false it only refreshes the stored
		/// orientations. Call it every frame so the stored orientation is always one frame old - otherwise the
		/// first sweep would span the whole wind-up back to the move's start pose.
		/// <paramref name="sweepStart"/> (0..1) lerps the sweep's START point from the previous orientation toward
		/// the current one - used on the first detection frame to begin exactly at the delay crossing rather than
		/// the full previous frame, keeping the detection start frame-rate/timescale independent. 0 = full previous
		/// frame (normal continuous sweep).
		/// </summary>
		public bool Update(bool scan, float sweepStart, out List<HitScanHitData> newHits)
		{
			newHits = new List<HitScanHitData>();

			foreach (Collider collider in colliders)
			{
				if (scan)
				{
					(Vector3 pos, Quaternion rot) start = orientations[collider];
					if (sweepStart > 0f)
					{
						start = (
							Vector3.Lerp(start.pos, collider.transform.position, sweepStart),
							Quaternion.Slerp(start.rot, collider.transform.rotation, sweepStart));
					}

					List<HitScanHitData> hits = CombatUtils.ColliderScan(agent.Targetable.Center, collider, start, DEFAULT_SCANS, layerMask);

					foreach (HitScanHitData hit in hits)
					{
						if (!hit.Transform.HasParent(agent.Transform) &&
							!newHits.Any(h => h.GameObject == hit.GameObject) &&
							!Hits.Any(h => h.GameObject == hit.GameObject))
						{
							newHits.Add(hit);
						}
					}
				}

				// Always refresh - even when not scanning - so the next sweep spans a single frame rather than
				// the stale gap back to the move's wind-up pose.
				orientations[collider] = (collider.transform.position, collider.transform.rotation);
			}

			if (scan)
			{
				Hits.AddRange(newHits);
				return newHits.Count > 0;
			}
			return false;
		}
	}
}
