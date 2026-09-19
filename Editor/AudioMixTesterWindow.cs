using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// A/B tool for the two mixing problems: LAYERS (different sounds at once, needing a raised exponent)
	/// and CROSSFADE (one sound at two forces, which must stay flat along the ladder). Plays 2D.
	/// </summary>
	public class AudioMixTesterWindow : EditorWindow
	{
		private enum Mode
		{
			Layers = 0,
			Crossfade = 1
		}

		private enum Reference
		{
			Lower = 0,
			Upper = 1,
			Nearest = 2
		}

		private const int MAX_LAYERS = 4;
		private static readonly float[] FLATNESS_STEPS = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };

		private Mode mode;

		private AudioClip reference;
		private List<AudioClip> layerClips = new List<AudioClip>(new AudioClip[3]);
		private List<float> layerWeights = new List<float>(new[] { 1f, 1f, 1f });

		private AudioClip lowerTier;
		private AudioClip upperTier;
		private float blend = 0.5f;
		private Reference compareAgainst = Reference.Nearest;
		private bool sweep;
		private float sweepStep = 0.1f;

		private float exponent = AudioMixUtils.EQUAL_POWER;
		private float share = AudioMixUtils.ENERGY_SHARE;
		private float interval = 1.2f;
		private bool looping;
		private bool playReferenceNext = true;
		private double nextPlayTime;

		private GameObject host;
		private AudioSource referenceSource;
		private List<AudioSource> layerSources = new List<AudioSource>();

		[MenuItem("Tools/Audio Mix Tester")]
		public static void OpenWindow()
		{
			AudioMixTesterWindow window = GetWindow<AudioMixTesterWindow>("Audio Mix");
			window.minSize = new Vector2(420f, 460f);
			window.Show();
		}

		protected void OnDisable()
		{
			looping = false;
			EditorApplication.update -= OnEditorUpdate;

			if (host != null)
			{
				DestroyImmediate(host);
			}
		}

		protected void OnGUI()
		{
			Mode previous = mode;
			mode = (Mode)EditorGUILayout.EnumPopup("Mode", mode);

			if (mode != previous)
			{
				// Each mode has a different right answer for the exponent; start from it.
				exponent = mode == Mode.Crossfade ? TieredSFX.BLEND_EXPONENT : 6f;
			}

			EditorGUILayout.Space();

			if (mode == Mode.Layers)
			{
				DrawLayers();
			}
			else
			{
				DrawCrossfade();
			}
		}

		#region Layers

		private void DrawLayers()
		{
			EditorGUILayout.HelpBox(
				"A is the reference clip at volume 1. B is the layers mixed under the settings below.\n" +
				"Exponent = how loud the set is overall. Share = how the makeup spreads across layers;\n" +
				"0.5 means a weight is a share of energy (half weight = -3dB), 1 means of amplitude (-6dB).\n" +
				"If you hear nothing, enable audio in the Scene view toolbar or enter play mode.",
				MessageType.None);

			EditorGUILayout.Space();
			reference = (AudioClip)EditorGUILayout.ObjectField("Reference (A)", reference, typeof(AudioClip), false);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Layers (B)", EditorStyles.boldLabel);

			for (int i = 0; i < layerClips.Count; i++)
			{
				EditorGUILayout.BeginHorizontal();
				layerClips[i] = (AudioClip)EditorGUILayout.ObjectField(layerClips[i], typeof(AudioClip), false);
				layerWeights[i] = EditorGUILayout.FloatField(layerWeights[i], GUILayout.Width(60f));
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.BeginHorizontal();
			using (new EditorGUI.DisabledScope(layerClips.Count >= MAX_LAYERS))
			{
				if (GUILayout.Button("Add layer"))
				{
					layerClips.Add(null);
					layerWeights.Add(1f);
				}
			}
			using (new EditorGUI.DisabledScope(layerClips.Count <= 1))
			{
				if (GUILayout.Button("Remove layer"))
				{
					layerClips.RemoveAt(layerClips.Count - 1);
					layerWeights.RemoveAt(layerWeights.Count - 1);
				}
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			exponent = EditorGUILayout.Slider("Mix exponent", exponent, AudioMixUtils.EQUAL_POWER, AudioMixUtils.DOMINANT);
			share = EditorGUILayout.Slider("Share (0.5 = energy)", share, AudioMixUtils.ENERGY_SHARE, AudioMixUtils.AMPLITUDE_SHARE);

			Dictionary<int, float> mix = BuildMix();
			EditorGUILayout.LabelField("Resulting volumes", string.Join("   ", FormatVolumes(mix)));

			DrawTransport(PlayReference, () => PlayMix(mix));
		}

		private Dictionary<int, float> BuildMix()
		{
			Dictionary<int, float> weights = new Dictionary<int, float>();
			for (int i = 0; i < layerClips.Count; i++)
			{
				if (layerClips[i] != null && layerWeights[i] > 0f)
				{
					weights[i] = layerWeights[i];
				}
			}

			return AudioMixUtils.NormalizedMix(weights, 1f, exponent, share);
		}

		private IEnumerable<string> FormatVolumes(Dictionary<int, float> mix)
		{
			foreach (KeyValuePair<int, float> entry in mix)
			{
				yield return $"{entry.Key}: {entry.Value:F2}";
			}
		}

		#endregion Layers

		#region Crossfade

		private void DrawCrossfade()
		{
			EditorGUILayout.HelpBox(
				"A is one tier alone at volume 1. B is both tiers crossfaded at the intensity below.\n" +
				"The ladder is right when B never sounds louder or quieter than A, at any intensity.\n" +
				"Exponent 2 is what TieredSFX ships; raise it and watch the readout bulge mid-ladder.",
				MessageType.None);

			EditorGUILayout.Space();
			lowerTier = (AudioClip)EditorGUILayout.ObjectField("Lower tier", lowerTier, typeof(AudioClip), false);
			upperTier = (AudioClip)EditorGUILayout.ObjectField("Upper tier", upperTier, typeof(AudioClip), false);

			EditorGUILayout.Space();
			blend = EditorGUILayout.Slider("Intensity", blend, 0f, 1f);
			exponent = EditorGUILayout.Slider("Blend exponent", exponent, AudioMixUtils.EQUAL_POWER, AudioMixUtils.DOMINANT);
			compareAgainst = (Reference)EditorGUILayout.EnumPopup("Compare against (A)", compareAgainst);

			Vector2 mix = CrossfadeMix(blend);
			EditorGUILayout.LabelField("Resulting volumes", $"lower: {mix.x:F2}   upper: {mix.y:F2}");

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Ladder flatness (dB vs one tier alone)", EditorStyles.boldLabel);
			EditorGUILayout.LabelField(" ", string.Join("   ", FormatFlatness()));

			EditorGUILayout.Space();
			sweep = EditorGUILayout.Toggle("Sweep intensity", sweep);
			using (new EditorGUI.DisabledScope(!sweep))
			{
				sweepStep = EditorGUILayout.Slider("Sweep step", sweepStep, 0.05f, 0.5f);
			}

			DrawTransport(PlayCrossfadeReference, PlayCrossfade);
		}

		/// <summary>The volumes <see cref="TieredSFX"/> resolves for a blend at <paramref name="t"/>.</summary>
		private Vector2 CrossfadeMix(float t)
		{
			return AudioMixUtils.NormalizedPair(1f - t, t, 1f, exponent);
		}

		/// <summary>
		/// Summed energy across the ladder; 0.0 at every step means the blend never bulges between tiers.
		/// </summary>
		private IEnumerable<string> FormatFlatness()
		{
			for (int i = 0; i < FLATNESS_STEPS.Length; i++)
			{
				Vector2 mix = CrossfadeMix(FLATNESS_STEPS[i]);
				float energy = mix.x * mix.x + mix.y * mix.y;
				float decibels = energy > 0.0001f ? 10f * Mathf.Log10(energy) : -80f;
				yield return $"{FLATNESS_STEPS[i]:0.00}: {decibels:+0.0;-0.0;0.0}";
			}
		}

		private AudioClip ReferenceTier()
		{
			switch (compareAgainst)
			{
				case Reference.Lower: return lowerTier;
				case Reference.Upper: return upperTier;
				default: return blend < 0.5f ? lowerTier : upperTier;
			}
		}

		private void PlayCrossfadeReference()
		{
			EnsureHost();
			AudioClip clip = ReferenceTier();

			if (clip == null)
			{
				return;
			}

			referenceSource.clip = clip;
			referenceSource.volume = 1f;
			referenceSource.Play();
		}

		private void PlayCrossfade()
		{
			EnsureHost();
			Vector2 mix = CrossfadeMix(blend);

			PlayOn(layerSources[0], lowerTier, mix.x);
			PlayOn(layerSources[1], upperTier, mix.y);

			if (sweep)
			{
				blend = blend >= 1f ? 0f : Mathf.Clamp01(blend + sweepStep);
				Repaint();
			}
		}

		private void PlayOn(AudioSource source, AudioClip clip, float volume)
		{
			if (clip == null || volume <= 0f)
			{
				return;
			}

			source.clip = clip;
			source.volume = volume;
			source.Play();
		}

		#endregion Crossfade

		#region Transport

		private void DrawTransport(Action playA, Action playB)
		{
			EditorGUILayout.Space();
			interval = EditorGUILayout.Slider("A/B interval (s)", interval, 0.3f, 4f);

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Play A"))
			{
				playA();
			}
			if (GUILayout.Button("Play B"))
			{
				playB();
			}
			if (GUILayout.Button(looping ? "Stop A/B" : "Loop A/B"))
			{
				ToggleLoop();
			}
			EditorGUILayout.EndHorizontal();
		}

		private void ToggleLoop()
		{
			looping = !looping;

			if (looping)
			{
				playReferenceNext = true;
				nextPlayTime = 0d;
				EditorApplication.update += OnEditorUpdate;
			}
			else
			{
				EditorApplication.update -= OnEditorUpdate;
			}
		}

		private void OnEditorUpdate()
		{
			if (EditorApplication.timeSinceStartup < nextPlayTime)
			{
				return;
			}

			if (playReferenceNext)
			{
				if (mode == Mode.Layers) { PlayReference(); } else { PlayCrossfadeReference(); }
			}
			else
			{
				if (mode == Mode.Layers) { PlayMix(BuildMix()); } else { PlayCrossfade(); }
			}

			playReferenceNext = !playReferenceNext;
			nextPlayTime = EditorApplication.timeSinceStartup + interval;
		}

		private void PlayReference()
		{
			EnsureHost();
			if (reference == null)
			{
				return;
			}

			referenceSource.clip = reference;
			referenceSource.volume = 1f;
			referenceSource.Play();
		}

		private void PlayMix(Dictionary<int, float> mix)
		{
			EnsureHost();

			foreach (KeyValuePair<int, float> entry in mix)
			{
				PlayOn(layerSources[entry.Key], layerClips[entry.Key], entry.Value);
			}
		}

		private void EnsureHost()
		{
			if (host == null)
			{
				host = new GameObject("AudioMixTester") { hideFlags = HideFlags.HideAndDontSave };
				referenceSource = CreateSource();
				layerSources.Clear();
			}

			while (layerSources.Count < Mathf.Max(layerClips.Count, 2))
			{
				layerSources.Add(CreateSource());
			}
		}

		private AudioSource CreateSource()
		{
			AudioSource source = host.AddComponent<AudioSource>();
			source.playOnAwake = false;
			source.spatialBlend = 0f;
			return source;
		}

		#endregion Transport
	}
}
