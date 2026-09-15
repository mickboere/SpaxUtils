using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Applies final per-renderer shader overrides using MaterialPropertyBlock.
	/// Does not instantiate materials. Does not do mixing or priority resolution.
	/// </summary>
	public sealed class MaterialEffectRenderer
	{
		private static readonly int EffectColorId = Shader.PropertyToID("_Effect_Color");
		private static readonly int EffectAmountId = Shader.PropertyToID("_Effect_Amount");
		private static readonly int AlphaFadeId = Shader.PropertyToID("_AlphaFade");
		private static readonly int HitAmplitudeId = Shader.PropertyToID("_HitAmplitude");
		private static readonly int HitShakeFloorId = Shader.PropertyToID("_HitShakeFloor");
		private static readonly int HitShakeHeightId = Shader.PropertyToID("_HitShakeHeight");
		private static readonly int HitPointId = Shader.PropertyToID("_HitPoint");
		private static readonly int HitWaveRadiusId = Shader.PropertyToID("_HitWaveRadius");
		private static readonly int HitDirectionId = Shader.PropertyToID("_HitDirection");
		private static readonly int HitWaveLengthId = Shader.PropertyToID("_HitWaveLength");
		private static readonly int HitWaveDecayId = Shader.PropertyToID("_HitWaveDecay");
		private static readonly int HitFalloffId = Shader.PropertyToID("_HitFalloff");
		private static readonly int HitWaveStretchId = Shader.PropertyToID("_HitWaveStretch");

		private readonly List<Renderer> renderers = new List<Renderer>();
		private readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

		private Color effectColor;
		private float effectAmount;
		private float alphaFade;
		private float shakeAmplitude;
		private float shakeFloor;
		private float shakeHeight;
		private Vector3 wavePoint;
		private Vector3 waveDirection = Vector3.forward;
		private float waveRadius;
		private float waveLength = 1f;
		private float waveDecay = 1f;
		private float waveFalloff = 1000f;
		private float waveStretch;

		public MaterialEffectRenderer()
		{
			effectColor = Color.black;
			effectAmount = 0f;
			alphaFade = 0f;
			shakeAmplitude = 0f;
			shakeFloor = 0f;
			shakeHeight = 1f;
		}

		public void SetRenderers(IEnumerable<Renderer> renderers)
		{
			this.renderers.Clear();

			foreach (Renderer renderer in renderers)
			{
				if (renderer != null)
				{
					this.renderers.Add(renderer);
				}
			}
		}

		public void SetEffectColor(Color color)
		{
			effectColor = color;
		}

		public void SetEffectAmount(float amount)
		{
			effectAmount = amount;
		}

		/// <summary>
		/// 0 = fully visible, 1 = fully faded.
		/// </summary>
		public void SetAlphaFade(float fade01)
		{
			alphaFade = Mathf.Clamp01(fade01);
		}

		/// <summary>
		/// Vertex offset strength along the hit direction: zero at <paramref name="floor"/>,
		/// full strength <paramref name="height"/> above it.
		/// </summary>
		public void SetShake(float amplitude, float floor, float height)
		{
			shakeAmplitude = amplitude;
			shakeFloor = floor;
			shakeHeight = height;
		}

		/// <summary>
		/// Ripple rings spreading from the axis through <paramref name="point"/> along <paramref name="direction"/>,
		/// front at <paramref name="radius"/>, fading out <paramref name="falloff"/> meters from the point.
		/// </summary>
		public void SetWave(Vector3 point, Vector3 direction, float radius,
			float length, float decay, float falloff, float stretch)
		{
			wavePoint = point;
			waveDirection = direction;
			waveRadius = radius;
			waveLength = length;
			waveDecay = decay;
			waveFalloff = falloff;
			waveStretch = stretch;
		}

		public void Apply()
		{
			for (int i = 0; i < renderers.Count; i++)
			{
				Renderer renderer = renderers[i];
				if (renderer == null)
				{
					continue;
				}

				renderer.GetPropertyBlock(mpb);

				mpb.SetColor(EffectColorId, effectColor);
				mpb.SetFloat(EffectAmountId, effectAmount);
				mpb.SetFloat(AlphaFadeId, alphaFade);
				mpb.SetFloat(HitAmplitudeId, shakeAmplitude);
				mpb.SetFloat(HitShakeFloorId, shakeFloor);
				mpb.SetFloat(HitShakeHeightId, shakeHeight);
				mpb.SetVector(HitPointId, wavePoint);
				mpb.SetVector(HitDirectionId, waveDirection);
				mpb.SetFloat(HitWaveRadiusId, waveRadius);
				mpb.SetFloat(HitWaveLengthId, waveLength);
				mpb.SetFloat(HitWaveDecayId, waveDecay);
				mpb.SetFloat(HitFalloffId, waveFalloff);
				mpb.SetFloat(HitWaveStretchId, waveStretch);

				renderer.SetPropertyBlock(mpb);
			}
		}
	}
}
