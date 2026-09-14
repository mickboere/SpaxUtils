using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// A/B tool for finding the mix exponent at which layered clips sound as loud as one clip at full volume.
	/// Plays 2D, so distance and rolloff are out of the picture.
	/// </summary>
	public class AudioMixTesterWindow : EditorWindow
	{
		private const int MAX_LAYERS = 4;

		private AudioClip reference;
		private List<AudioClip> layerClips = new List<AudioClip>(new AudioClip[3]);
		private List<float> layerWeights = new List<float>(new[] { 1f, 1f, 1f });

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
			window.minSize = new Vector2(420f, 420f);
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

			EditorGUILayout.Space();
			interval = EditorGUILayout.Slider("A/B interval (s)", interval, 0.3f, 4f);

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Play A"))
			{
				PlayReference();
			}
			if (GUILayout.Button("Play B"))
			{
				PlayMix(mix);
			}
			if (GUILayout.Button(looping ? "Stop A/B" : "Loop A/B"))
			{
				ToggleLoop();
			}
			EditorGUILayout.EndHorizontal();
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
				PlayReference();
			}
			else
			{
				PlayMix(BuildMix());
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
				AudioSource source = layerSources[entry.Key];
				source.clip = layerClips[entry.Key];
				source.volume = entry.Value;
				source.Play();
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

			while (layerSources.Count < layerClips.Count)
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
	}
}
