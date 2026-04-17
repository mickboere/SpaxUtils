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

		private readonly List<Renderer> renderers = new List<Renderer>();
		private readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

		private Color effectColor;
		private float effectAmount;
		private float alphaFade;

		public MaterialEffectRenderer()
		{
			effectColor = Color.black;
			effectAmount = 0f;
			alphaFade = 0f;
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

				renderer.SetPropertyBlock(mpb);
			}
		}
	}
}
