using System.Collections.Generic;
using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// Mixes appearance effect requests (id, prio, weight) into final shader values.
	/// Shared by <see cref="EntityAppearanceEffectHandler"/> and the editor preview.
	/// </summary>
	public class AppearanceEffectMixer
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

		/// <summary>
		/// Whether any request changed since the last <see cref="ApplyTo"/>.
		/// </summary>
		public bool Dirty { get; set; } = true;

		private readonly Dictionary<object, FlashRequest> flashRequests = new Dictionary<object, FlashRequest>();
		private readonly Dictionary<object, FadeRequest> fadeRequests = new Dictionary<object, FadeRequest>();
		private readonly Dictionary<object, float> amplitudeRequests = new Dictionary<object, float>();

		private float offsetFloor;
		private float offsetHeight = 1f;
		private Vector3 wavePoint;
		private Vector3 waveDirection = Vector3.forward;
		private float waveRadius;
		private float waveLength = 1f;
		private float waveDecay = 1f;
		private float waveFalloff = 1000f;
		private float waveStretch;

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

		/// <summary>
		/// Requests vertex offset strength along the wave direction; requests sum.
		/// Zero at <paramref name="floor"/>, full strength <paramref name="height"/> above it.
		/// </summary>
		public void RequestAmplitude(object id, float amplitude, float floor, float height)
		{
			if (id == null)
			{
				return;
			}

			amplitudeRequests[id] = amplitude;
			offsetFloor = floor;
			offsetHeight = height;
			Dirty = true;
		}

		/// <summary>
		/// Sets the ripple the vertex offset rides on; a newer hit replaces the running wave.
		/// </summary>
		public void RequestWave(Vector3 point, Vector3 direction, float radius,
			float length, float decay, float falloff, float stretch)
		{
			wavePoint = point;
			waveDirection = direction;
			waveRadius = radius;
			waveLength = length;
			waveDecay = decay;
			waveFalloff = falloff;
			waveStretch = stretch;
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
			removed |= amplitudeRequests.Remove(id);

			if (removed)
			{
				Dirty = true;
			}
		}

		public void ClearAll()
		{
			flashRequests.Clear();
			fadeRequests.Clear();
			amplitudeRequests.Clear();
			Dirty = true;
		}

		/// <summary>
		/// Writes the mixed result into <paramref name="renderer"/>; call its Apply afterwards.
		/// </summary>
		public void ApplyTo(MaterialEffectRenderer renderer)
		{
			CalculateFlash(out Color flashColor, out float flashAmount);
			CalculateFade(out float alphaFade);

			renderer.SetEffectColor(flashColor);
			renderer.SetEffectAmount(flashAmount);
			renderer.SetAlphaFade(alphaFade);
			renderer.SetShake(CalculateAmplitude(), offsetFloor, offsetHeight);
			renderer.SetWave(wavePoint, waveDirection, waveRadius,
				waveLength, waveDecay, waveFalloff, waveStretch);

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

		private float CalculateAmplitude()
		{
			float sum = 0f;

			foreach (KeyValuePair<object, float> kv in amplitudeRequests)
			{
				sum += kv.Value;
			}

			return sum;
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
