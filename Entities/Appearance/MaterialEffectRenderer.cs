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
		private static readonly int PinFloorId = Shader.PropertyToID("_DeformPinFloor");
		private static readonly int PinHeightId = Shader.PropertyToID("_DeformPinHeight");
		private static readonly int RipplePointId = Shader.PropertyToID("_DeformRipplePoint");
		private static readonly int RippleDirectionId = Shader.PropertyToID("_DeformRippleDirection");
		private static readonly int RippleAmplitudeId = Shader.PropertyToID("_DeformRippleAmplitude");
		private static readonly int RippleRadiusId = Shader.PropertyToID("_DeformRippleRadius");
		private static readonly int RippleLengthId = Shader.PropertyToID("_DeformRippleLength");
		private static readonly int RippleDecayId = Shader.PropertyToID("_DeformRippleDecay");
		private static readonly int RippleFalloffId = Shader.PropertyToID("_DeformRippleFalloff");
		private static readonly int RippleStretchId = Shader.PropertyToID("_DeformRippleStretch");
		private static readonly int RippleOutwardId = Shader.PropertyToID("_DeformRippleOutward");
		private static readonly int RippleReboundId = Shader.PropertyToID("_DeformRippleRebound");
		private static readonly int SmearId = Shader.PropertyToID("_DeformSmear");
		private static readonly int SmearOriginId = Shader.PropertyToID("_DeformSmearOrigin");
		private static readonly int SmearFalloffId = Shader.PropertyToID("_DeformSmearFalloff");
		private static readonly int SmearPhaseId = Shader.PropertyToID("_DeformSmearPhase");
		private static readonly int SmearStreakScaleId = Shader.PropertyToID("_DeformSmearStreakScale");
		private static readonly int SmearStreakAmountId = Shader.PropertyToID("_DeformSmearStreakAmount");
		private static readonly int SmearDitherId = Shader.PropertyToID("_DeformSmearDither");

		private readonly List<Renderer> renderers = new List<Renderer>();
		private readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

		private Color effectColor;
		private float effectAmount;
		private float alphaFade;
		private float pinFloor;
		private float pinHeight;
		private DeformRipple ripple;
		private DeformSmear smear;

		public MaterialEffectRenderer()
		{
			effectColor = Color.black;
			effectAmount = 0f;
			alphaFade = 0f;
			pinFloor = 0f;
			pinHeight = 1f;
			smear.Falloff = 1f;
			smear.StreakScale = 1f;
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
		/// Deformation is zero at <paramref name="floor"/> and full <paramref name="height"/> above it.
		/// </summary>
		public void SetPin(float floor, float height)
		{
			pinFloor = floor;
			pinHeight = height;
		}

		public void SetRipple(DeformRipple ripple)
		{
			this.ripple = ripple;
		}

		public void SetSmear(DeformSmear smear)
		{
			this.smear = smear;
		}

		/// <summary>
		/// Writes the feet pin and smear into <paramref name="mpb"/>; shared with trail snapshots.
		/// </summary>
		public static void SetSmearProperties(MaterialPropertyBlock mpb, DeformSmear smear, float floor, float height)
		{
			mpb.SetFloat(PinFloorId, floor);
			mpb.SetFloat(PinHeightId, height);
			mpb.SetVector(SmearId, smear.Vector);
			mpb.SetVector(SmearOriginId, smear.Origin);
			mpb.SetFloat(SmearFalloffId, smear.Falloff);
			mpb.SetFloat(SmearPhaseId, smear.Phase);
			mpb.SetFloat(SmearStreakScaleId, smear.StreakScale);
			mpb.SetFloat(SmearStreakAmountId, smear.StreakAmount);
			mpb.SetFloat(SmearDitherId, smear.Dither);
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
				SetSmearProperties(mpb, smear, pinFloor, pinHeight);
				mpb.SetVector(RipplePointId, ripple.Point);
				mpb.SetVector(RippleDirectionId, ripple.Direction);
				mpb.SetFloat(RippleAmplitudeId, ripple.Amplitude);
				mpb.SetFloat(RippleRadiusId, ripple.Radius);
				mpb.SetFloat(RippleLengthId, ripple.Length);
				mpb.SetFloat(RippleDecayId, ripple.Decay);
				mpb.SetFloat(RippleFalloffId, ripple.Falloff);
				mpb.SetFloat(RippleStretchId, ripple.Stretch);
				mpb.SetFloat(RippleOutwardId, ripple.Outward);
				mpb.SetFloat(RippleReboundId, ripple.Rebound);

				renderer.SetPropertyBlock(mpb);
			}
		}
	}
}
