using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour for melee <see cref="IMeleeCombatMove"/>s that manages weapon trail effects during performance.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatBehaviour_WeaponTrail", menuName = "ScriptableObjects/Combat/WeaponTrailBehaviourAsset")]
	public class WeaponTrailBehaviourAsset : BasePerformanceMoveBehaviourAsset
	{
		[SerializeField] private PooledWeaponTrail trailPrefab;
		[SerializeField] private float duration = 0.2f;
		[SerializeField] private PerformanceState state;

		private GlobalPoolingManager globalPoolingManager;
		private TransformLookup transformLookup;

		private Pool<PooledWeaponTrail> trailPool;
		private PooledWeaponTrail lastTrail;
		private WeaponComponent weapon;
		private bool initialized;

		public void InjectDependencies(GlobalPoolingManager globalPoolingManager, TransformLookup transformLookup)
		{
			this.globalPoolingManager = globalPoolingManager;
			this.transformLookup = transformLookup;
		}

		public override void Start()
		{
			base.Start();

			if (Move is IMeleeCombatMove meleeMove)
			{
				foreach (string hitBox in meleeMove.HitBoxes)
				{
					Transform transform = transformLookup.Lookup(hitBox);
					if (transform != null && transform.gameObject.TryGetComponentInChildren(out weapon))
					{
						break;
					}
				}
			}

			if (weapon == null)
			{
				// Cannot instance a trail if there is no weapon to reference.
				return;
			}

			trailPool = globalPoolingManager.Request(trailPrefab);
			Performer.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			initialized = false;
		}

		public override void Stop()
		{
			base.Stop();

			Performer.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
		}

		protected void OnPerformanceUpdateEvent(IPerformer performer)
		{
			if (state.HasFlag(performer.State) && !initialized)
			{
				// Initialize.
				if (lastTrail != null && lastTrail.WeaponTrail.Cast)
				{
					lastTrail.WeaponTrail.Cast = false;
				}

				lastTrail = trailPool.Request();
				lastTrail.WeaponTrail.Bottom = weapon.Base;
				lastTrail.WeaponTrail.Top = weapon.Tip;
				lastTrail.WeaponTrail.Duration = duration;
				lastTrail.WeaponTrail.Cast = true;
				initialized = true;
			}

			if (lastTrail != null && lastTrail.WeaponTrail.Cast &&
				performer.State is PerformanceState.Finishing or PerformanceState.Completed)
			{
				lastTrail.WeaponTrail.Cast = false;
				lastTrail = null;
			}
		}
	}
}
