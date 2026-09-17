using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// Vertex deformation on an entity: one-shot ripples and held smears, optionally following a bone.
	/// </summary>
	[DefaultExecutionOrder(110)]
	public class EntityDeformer : EntityComponentMono
	{
		private EntityAppearanceEffectHandler appearanceEffects;
		private ITargetable targetable;
		private DeformationPlayer _player;

		// Lazy: Transform resolves through the entity, which may not be linked during injection.
		private DeformationPlayer player => _player ??= appearanceEffects != null
			? new DeformationPlayer(Transform, targetable, appearanceEffects)
			: null;

		public void InjectDependencies(EntityAppearanceEffectHandler appearanceEffects,
			[Optional] ITargetable targetable)
		{
			this.appearanceEffects = appearanceEffects;
			this.targetable = targetable;
		}

		protected void Update()
		{
			// Global time, not entity timescale: keeps animating through the hit-pause.
			_player?.Update(Time.deltaTime);
		}

		protected void OnDestroy()
		{
			_player?.ClearAll();
		}

		#region Deformation

		/// <inheritdoc cref="DeformationPlayer.PlayRipple"/>
		public void PlayRipple(RippleProfile profile, Vector3 point, Vector3 direction, float intensity)
		{
			player?.PlayRipple(profile, point, direction, intensity);
		}

		public void StopRipple()
		{
			player?.StopRipple();
		}

		/// <inheritdoc cref="DeformationPlayer.SetSmear(object, SmearProfile, Vector3, Vector3, float)"/>
		public void SetSmear(object id, SmearProfile profile, Vector3 origin, Vector3 direction, float intensity)
		{
			player?.SetSmear(id, profile, origin, direction, intensity);
		}

		/// <inheritdoc cref="DeformationPlayer.SetSmear(object, SmearProfile, Transform, float)"/>
		public void SetSmear(object id, SmearProfile profile, Transform follow, float intensity)
		{
			player?.SetSmear(id, profile, follow, intensity);
		}

		public void SetSmearIntensity(object id, float intensity)
		{
			player?.SetSmearIntensity(id, intensity);
		}

		public void ClearSmear(object id)
		{
			player?.ClearSmear(id);
		}

		#endregion Deformation
	}
}
