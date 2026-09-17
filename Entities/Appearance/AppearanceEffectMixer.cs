using System.Collections.Generic;
using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// Mixes appearance effect requests (id, prio, weight) into final shader values.
	/// Shared by <see cref="EntityAppearanceEffectHandler"/> and the editor preview.
	/// </summary>
	public class AppearanceEffectMixer : IAppearanceEffects
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

		private struct RippleRequest
		{
			public DeformRipple ripple;
			public float floor;
			public float height;
			public int order;
		}

		private struct SmearRequest
		{
			public DeformSmear smear;
			public float floor;
			public float height;
		}

		/// <summary>
		/// Whether any request changed since the last <see cref="ApplyTo"/>.
		/// </summary>
		public bool Dirty { get; set; } = true;

		private readonly Dictionary<object, FlashRequest> flashRequests = new Dictionary<object, FlashRequest>();
		private readonly Dictionary<object, FadeRequest> fadeRequests = new Dictionary<object, FadeRequest>();
		private readonly Dictionary<object, RippleRequest> rippleRequests = new Dictionary<object, RippleRequest>();
		private readonly Dictionary<object, SmearRequest> smearRequests = new Dictionary<object, SmearRequest>();

		private int rippleOrder;

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
			Dirty = true;
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
			Dirty = true;
		}

		/// <inheritdoc/>
		public void RequestRipple(object id, DeformRipple ripple, float floor, float height)
		{
			if (id == null)
			{
				return;
			}

			int order = rippleRequests.TryGetValue(id, out RippleRequest existing) ? existing.order : ++rippleOrder;
			rippleRequests[id] = new RippleRequest { ripple = ripple, floor = floor, height = height, order = order };
			Dirty = true;
		}

		/// <inheritdoc/>
		public void RequestSmear(object id, DeformSmear smear, float floor, float height)
		{
			if (id == null)
			{
				return;
			}

			smearRequests[id] = new SmearRequest { smear = smear, floor = floor, height = height };
			Dirty = true;
		}

		public void Clear(object id)
		{
			if (id == null)
			{
				return;
			}

			bool removed = flashRequests.Remove(id);
			removed |= fadeRequests.Remove(id);
			removed |= rippleRequests.Remove(id);
			removed |= smearRequests.Remove(id);

			if (removed)
			{
				Dirty = true;
			}
		}

		/// <summary>
		/// The smear and feet pin the current requests resolve to.
		/// </summary>
		public void GetSmear(out DeformSmear smear, out float floor, out float height)
		{
			smear = CalculateSmear();
			CalculatePin(out floor, out height);
		}

		public void ClearAll()
		{
			flashRequests.Clear();
			fadeRequests.Clear();
			rippleRequests.Clear();
			smearRequests.Clear();
			Dirty = true;
		}

		/// <summary>
		/// Writes the mixed result into <paramref name="renderer"/>; call its Apply afterwards.
		/// </summary>
		public void ApplyTo(MaterialEffectRenderer renderer)
		{
			CalculateFlash(out Color flashColor, out float flashAmount);
			CalculateFade(out float alphaFade);
			CalculatePin(out float floor, out float height);

			renderer.SetEffectColor(flashColor);
			renderer.SetEffectAmount(flashAmount);
			renderer.SetAlphaFade(alphaFade);
			renderer.SetPin(floor, height);
			renderer.SetRipple(CalculateRipple());
			renderer.SetSmear(CalculateSmear());

			Dirty = false;
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

		private DeformRipple CalculateRipple()
		{
			DeformRipple result = new DeformRipple();
			int latest = int.MinValue;

			foreach (KeyValuePair<object, RippleRequest> kv in rippleRequests)
			{
				if (kv.Value.order > latest)
				{
					latest = kv.Value.order;
					result = kv.Value.ripple;
				}
			}

			return result;
		}

		/// <summary>
		/// One pin for every offset: the lowest floor and highest top any request asks for.
		/// </summary>
		private void CalculatePin(out float floor, out float height)
		{
			bool any = false;
			float low = 0f;
			float top = 1f;

			void Include(float requestFloor, float requestHeight)
			{
				float requestTop = requestFloor + requestHeight;
				low = any ? Mathf.Min(low, requestFloor) : requestFloor;
				top = any ? Mathf.Max(top, requestTop) : requestTop;
				any = true;
			}

			foreach (KeyValuePair<object, RippleRequest> kv in rippleRequests)
			{
				Include(kv.Value.floor, kv.Value.height);
			}

			foreach (KeyValuePair<object, SmearRequest> kv in smearRequests)
			{
				Include(kv.Value.floor, kv.Value.height);
			}

			floor = low;
			height = Mathf.Max(top - low, 0.01f);
		}

		private DeformSmear CalculateSmear()
		{
			DeformSmear result = new DeformSmear { Falloff = 1f, StreakScale = 1f };
			Vector3 sum = Vector3.zero;
			float strongest = -1f;

			foreach (KeyValuePair<object, SmearRequest> kv in smearRequests)
			{
				DeformSmear smear = kv.Value.smear;
				sum += smear.Vector;

				float strength = smear.Vector.sqrMagnitude;
				if (strength > strongest)
				{
					strongest = strength;
					result = smear;
				}
			}

			result.Vector = sum;
			return result;
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
