using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// Collects appearance effect requests into an <see cref="AppearanceEffectMixer"/>.
	/// Applies results to all active visual renderers using MaterialPropertyBlock.
	/// </summary>
	[DefaultExecutionOrder(60)]
	public class EntityAppearanceEffectHandler : EntityComponentMono
	{
		private EntityAppearanceHandler appearanceHandler;
		private MaterialEffectRenderer effectRenderer;
		private readonly AppearanceEffectMixer mixer = new AppearanceEffectMixer();

		public void InjectDependencies(EntityAppearanceHandler appearanceHandler)
		{
			this.appearanceHandler = appearanceHandler;
		}

		protected void Start()
		{
			effectRenderer = new MaterialEffectRenderer();

			if (appearanceHandler != null)
			{
				appearanceHandler.UpdatedActiveRenderersEvent += OnUpdatedActiveRenderersEvent;
				OnUpdatedActiveRenderersEvent();
			}

			mixer.Dirty = true;
		}

		protected void OnDestroy()
		{
			if (appearanceHandler != null)
			{
				appearanceHandler.UpdatedActiveRenderersEvent -= OnUpdatedActiveRenderersEvent;
			}
		}

		protected void LateUpdate()
		{
			if (!mixer.Dirty)
			{
				return;
			}

			mixer.ApplyTo(effectRenderer);
			effectRenderer.Apply();
		}

		#region Requests

		public void RequestFlash(object id, int prio, float weight, Color color, float amount)
		{
			mixer.RequestFlash(id, prio, weight, color, amount);
		}

		public void RequestFade(object id, int prio, float weight, float fade01)
		{
			mixer.RequestFade(id, prio, weight, fade01);
		}

		/// <inheritdoc cref="AppearanceEffectMixer.RequestAmplitude"/>
		public void RequestAmplitude(object id, float amplitude, float floor, float height)
		{
			mixer.RequestAmplitude(id, amplitude, floor, height);
		}

		/// <inheritdoc cref="AppearanceEffectMixer.RequestWave"/>
		public void RequestWave(Vector3 point, Vector3 direction, float radius,
			float length, float decay, float falloff, float stretch)
		{
			mixer.RequestWave(point, direction, radius, length, decay, falloff, stretch);
		}

		public void Clear(object id)
		{
			mixer.Clear(id);
		}

		#endregion Requests

		private void OnUpdatedActiveRenderersEvent()
		{
			if (appearanceHandler == null)
			{
				return;
			}

			effectRenderer.SetRenderers(appearanceHandler.ActiveVisualRenderers);
			mixer.Dirty = true;
		}
	}
}
