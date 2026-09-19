using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// A ladder of <see cref="SFXTier"/>s selected by intensity, owning its own mixing and playback.
	/// </summary>
	[Serializable]
	public class TieredSFX
	{
		public enum PlayMode
		{
			/// <summary>Crossfades the tiers bracketing the intensity; below the ladder the softest fades in.</summary>
			Blend = 0,
			/// <summary>Plays only the loudest tier at or below the intensity; below the ladder, silence.</summary>
			Single = 1
		}

		/// <summary>One resolved rung of a mix; carries all a later play still needs.</summary>
		public struct Layer
		{
			public SFXTier Tier;
			public SFXSettings Settings;
			public float Volume;
			public float Intensity;
		}

		/// <summary>
		/// Tiers are one sound at two forces, so their crossfade must hold loudness flat along the ladder.
		/// Verify by ear under Tools > Audio Mix Tester.
		/// </summary>
		public const float BLEND_EXPONENT = AudioMixUtils.EQUAL_POWER;

		/// <summary>Layers below this volume are dropped rather than burn an audio source.</summary>
		private const float CULL_VOLUME = 0.05f;

		public PlayMode Mode => mode;
		public SFXSettings Defaults => defaults;
		public IReadOnlyList<SFXTier> Tiers => tiers;

		public bool HasTiers
		{
			get
			{
				if (tiers == null)
				{
					return false;
				}

				for (int i = 0; i < tiers.Count; i++)
				{
					if (tiers[i] != null && tiers[i].HasClips)
					{
						return true;
					}
				}

				return false;
			}
		}

		[SerializeField] private PlayMode mode = PlayMode.Blend;
		[SerializeField] private SFXSettings defaults = new SFXSettings();
		[SerializeField] private List<SFXTier> tiers = new List<SFXTier>();

		[NonSerialized] private List<Layer> buffer = new List<Layer>();

		#region Playing

		/// <summary>
		/// Plays the ladder at <paramref name="intensity"/>, drawing one source per layer from <paramref name="source"/>.
		/// </summary>
		/// <param name="roll">Shared position within the volume and pitch ranges; negative draws one at random.</param>
		public void Play(float intensity, Func<AudioSourceWrapper> source, float volume = 1f,
			float pitch = 1f, float distance = 1f, float roll = -1f)
		{
			GetLayers(intensity, buffer);

			if (buffer.Count == 0)
			{
				return;
			}

			// One roll for the whole ladder: its layers are the same event, not separate sounds.
			roll = roll < 0f ? UnityEngine.Random.value : Mathf.Clamp01(roll);

			for (int i = 0; i < buffer.Count; i++)
			{
				Play(buffer[i], source, volume, pitch, distance, roll);
			}

			buffer.Clear();
		}

		/// <summary>Plays a single layer resolved earlier by <see cref="GetLayers"/>.</summary>
		public static void Play(Layer layer, Func<AudioSourceWrapper> source, float volume = 1f,
			float pitch = 1f, float distance = 1f, float roll = -1f)
		{
			AudioClip clip = layer.Tier == null ? null : layer.Tier.NextClip();
			AudioSourceWrapper wrapper = clip == null || source == null ? null : source.Invoke();

			if (wrapper == null || layer.Settings == null)
			{
				return;
			}

			roll = roll < 0f ? UnityEngine.Random.value : Mathf.Clamp01(roll);

			SFXPlayer.Play(wrapper, clip,
				layer.Settings.VolumeRange.Lerp(roll) * volume * layer.Volume,
				layer.Settings.PitchRange.Lerp(roll) * pitch,
				layer.Settings.MinDistance(layer.Intensity) * distance,
				layer.Settings.MaxDistance(layer.Intensity) * distance);
		}

		/// <summary>
		/// Resolves the ladder at <paramref name="intensity"/> into layers carrying energy-share volumes.
		/// </summary>
		public void GetLayers(float intensity, List<Layer> layers)
		{
			layers.Clear();

			if (tiers == null)
			{
				return;
			}

			SFXTier lower = null;
			SFXTier upper = null;
			SFXTier softest = null;

			for (int i = 0; i < tiers.Count; i++)
			{
				SFXTier tier = tiers[i];
				if (tier == null || !tier.HasClips)
				{
					continue;
				}

				if (softest == null || tier.Intensity < softest.Intensity)
				{
					softest = tier;
				}

				if (tier.Intensity <= intensity)
				{
					if (lower == null || tier.Intensity > lower.Intensity)
					{
						lower = tier;
					}
				}
				else if (upper == null || tier.Intensity < upper.Intensity)
				{
					upper = tier;
				}
			}

			if (lower == null)
			{
				// Below the ladder: Blend owns loudness so it fades the softest rung in, Single waits for it.
				if (mode != PlayMode.Single && softest != null)
				{
					float approach = softest.Intensity > 0f ? Mathf.Clamp01(intensity / softest.Intensity) : 1f;
					AddLayer(layers, softest, AudioMixUtils.Amplitude(approach), intensity);
				}

				return;
			}

			if (mode == PlayMode.Single || upper == null)
			{
				AddLayer(layers, lower, 1f, intensity);
				return;
			}

			float t = Mathf.InverseLerp(lower.Intensity, upper.Intensity, intensity);
			Vector2 mix = AudioMixUtils.NormalizedPair(1f - t, t, 1f, BLEND_EXPONENT);

			AddLayer(layers, lower, mix.x, intensity);
			AddLayer(layers, upper, mix.y, intensity);
		}

		private void AddLayer(List<Layer> layers, SFXTier tier, float volume, float intensity)
		{
			if (tier == null || volume < CULL_VOLUME)
			{
				return;
			}

			layers.Add(new Layer
			{
				Tier = tier,
				Settings = tier.Resolve(defaults),
				Volume = volume,
				Intensity = Mathf.Clamp01(intensity)
			});
		}

		#endregion Playing
	}
}
