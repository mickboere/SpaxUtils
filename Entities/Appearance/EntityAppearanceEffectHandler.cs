using System.Collections.Generic;
using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// Collects appearance effect requests (id, prio, weight) and mixes them into final shader values.
	/// Applies results to all active visual renderers using MaterialPropertyBlock.
	/// </summary>
	[DefaultExecutionOrder(60)]
	public class EntityAppearanceEffectHandler : EntityComponentMono
	{
		private struct FlashRequest
		{
			public int prio;
			public float weight;
			public Color color;
			public float amount;
		}

		private struct FadeRequest
		{
			public int prio;
			public float weight;
			public float fade;
		}

		private EntityAppearanceHandler appearanceHandler;
		private MaterialEffectRenderer effectRenderer;

		private Dictionary<object, FlashRequest> flashRequests = new Dictionary<object, FlashRequest>();
		private Dictionary<object, FadeRequest> fadeRequests = new Dictionary<object, FadeRequest>();

		private bool dirty;

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

			dirty = true;
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
			if (!dirty)
			{
				return;
			}

			dirty = false;

			Color flashColor;
			float flashAmount;
			float alphaFade;

			CalculateFlash(out flashColor, out flashAmount);
			CalculateFade(out alphaFade);

			effectRenderer.SetEffectColor(flashColor);
			effectRenderer.SetEffectAmount(flashAmount);
			effectRenderer.SetAlphaFade(alphaFade);
			effectRenderer.Apply();
		}

		public void RequestFlash(object id, int prio, float weight, Color color, float amount)
		{
			if (id == null)
			{
				return;
			}

			FlashRequest req = new FlashRequest();
			req.prio = prio;
			req.weight = Mathf.Clamp01(weight);
			req.color = color;
			req.amount = Mathf.Clamp01(amount);

			flashRequests[id] = req;
			dirty = true;
		}

		public void RequestFade(object id, int prio, float weight, float fade01)
		{
			if (id == null)
			{
				return;
			}

			FadeRequest req = new FadeRequest();
			req.prio = prio;
			req.weight = Mathf.Clamp01(weight);
			req.fade = Mathf.Clamp01(fade01);

			fadeRequests[id] = req;
			dirty = true;
		}

		public void Clear(object id)
		{
			if (id == null)
			{
				return;
			}

			bool removed = false;

			if (flashRequests.Remove(id))
			{
				removed = true;
			}

			if (fadeRequests.Remove(id))
			{
				removed = true;
			}

			if (removed)
			{
				dirty = true;
			}
		}

		private void OnUpdatedActiveRenderersEvent()
		{
			if (appearanceHandler == null)
			{
				return;
			}

			effectRenderer.SetRenderers(appearanceHandler.ActiveVisualRenderers);
			dirty = true;
		}

		private void CalculateFlash(out Color color, out float amount)
		{
			List<FlashRequest> list = new List<FlashRequest>(flashRequests.Count + 1);

			FlashRequest baseReq = new FlashRequest();
			baseReq.prio = int.MinValue;
			baseReq.weight = 1f;
			baseReq.color = Color.black;
			baseReq.amount = 0f;
			list.Add(baseReq);

			foreach (KeyValuePair<object, FlashRequest> kv in flashRequests)
			{
				list.Add(kv.Value);
			}

			list.Sort(CompareFlash);

			int topPrio = list[0].prio;
			float totalWeight = 0f;
			float totalContribution = 0f;
			Vector4 colorAccum = Vector4.zero;

			for (int i = 0; i < list.Count; i++)
			{
				FlashRequest req = list[i];
				float w = req.weight;

				if (w < 0.001f)
				{
					continue;
				}

				if (req.prio < topPrio)
				{
					if (totalWeight < 1f)
					{
						w *= (1f - totalWeight);
					}
					else
					{
						w = 0f;
					}
				}

				if (w < 0.001f)
				{
					continue;
				}

				float contrib = w * Mathf.Clamp01(req.amount);

				if (contrib < 0.001f)
				{
					totalWeight += w;
					continue;
				}

				colorAccum += (Vector4)req.color * contrib;
				totalContribution += contrib;
				totalWeight += w;

				if (totalWeight >= 1f)
				{
					break;
				}
			}

			amount = Mathf.Clamp01(totalContribution);

			if (totalContribution > 0.0001f)
			{
				Vector4 c = colorAccum / totalContribution;
				color = new Color(c.x, c.y, c.z, 1f);
			}
			else
			{
				color = Color.black;
			}
		}

		private void CalculateFade(out float fade)
		{
			float visibility = 1f;

			foreach (KeyValuePair<object, FadeRequest> kv in fadeRequests)
			{
				FadeRequest req = kv.Value;

				float w = req.weight;
				if (w < 0.001f)
				{
					continue;
				}

				float f = Mathf.Clamp01(req.fade);
				float v = 1f - f;

				if (v <= 0.00001f)
				{
					visibility = 0f;
					break;
				}

				visibility *= Mathf.Pow(v, w);

				if (visibility <= 0.00001f)
				{
					visibility = 0f;
					break;
				}
			}

			fade = Mathf.Clamp01(1f - visibility);
		}

		private static int CompareFlash(FlashRequest a, FlashRequest b)
		{
			if (a.prio != b.prio)
			{
				return b.prio.CompareTo(a.prio);
			}

			return b.weight.CompareTo(a.weight);
		}
	}
}
